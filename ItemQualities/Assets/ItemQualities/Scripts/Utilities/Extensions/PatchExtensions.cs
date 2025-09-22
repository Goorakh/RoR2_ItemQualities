using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ItemQualities.Utilities.Extensions
{
    public static class PatchExtensions
    {
        /// <summary>
        /// Emits instructions to unconditionally skip the method call directly ahead of the cursor and moves after it.
        /// </summary>
        /// <param name="c">The cursor to emit instructions with</param>
        /// <remarks>
        /// If the skipped method has a return value, the default value for that type is placed on the stack when the call is skipped.
        /// <para/>
        /// This method will use an unconditional branch <see cref="OpCode"/> (<see cref="OpCodes.Br"/>). This is equivalent to removing the method call completely, however the call still exists in IL.
        /// </remarks>
        public static void EmitSkipMethodCall(this ILCursor c)
        {
            EmitSkipMethodCall(c, OpCodes.Br);
        }

        /// <summary>
        /// Emits instructions to skip the method call directly ahead of the cursor and moves after it.
        /// </summary>
        /// <param name="c">The cursor to emit instructions with</param>
        /// <param name="branchOpCode">The branch <see cref="OpCode"/> to emit, can be a conditional or unconditional branch</param>
        /// <remarks>If the skipped method has a return value, the default value for that type is placed on the stack when the call is skipped</remarks>
        public static void EmitSkipMethodCall(this ILCursor c, OpCode branchOpCode)
        {
            EmitSkipMethodCall(c, branchOpCode, null);
        }

        /// <summary>
        /// Emits instructions to skip the method call directly ahead of the cursor and moves after it.
        /// </summary>
        /// <param name="c">The cursor to emit instructions with</param>
        /// <param name="emitSkippedReturnValue">If the skipped method is not <see langword="void"/> this delegate will be invoked to emit the missing return value.
        /// <para/>
        public static void EmitSkipMethodCall(this ILCursor c, Action<ILCursor> emitSkippedReturnValue)
        {
            EmitSkipMethodCall(c, OpCodes.Br, emitSkippedReturnValue);
        }

        /// <summary>
        /// Emits instructions to skip the method call directly ahead of the cursor and moves after it.
        /// </summary>
        /// <param name="c">The cursor to emit instructions with</param>
        /// <param name="branchOpCode">The branch <see cref="OpCode"/> to emit, can be a conditional or unconditional branch</param>
        /// <param name="emitSkippedReturnValue">If the skipped method is not <see langword="void"/> this delegate will be invoked to emit the missing return value.
        /// <para/>
        /// Can be <see langword="null"/>, in which case the default value of the return type will be emitted instead</param>
        /// <exception cref="ArgumentNullException"><paramref name="c"/> is <see langword="null"/></exception>
        /// <exception cref="ArgumentException"><paramref name="branchOpCode"/> is not a valid branch <see cref="OpCode"/></exception>
        public static void EmitSkipMethodCall(this ILCursor c, OpCode branchOpCode, Action<ILCursor> emitSkippedReturnValue)
        {
            if (c is null)
                throw new ArgumentNullException(nameof(c));

            if (branchOpCode.FlowControl != FlowControl.Branch && branchOpCode.FlowControl != FlowControl.Cond_Branch)
                throw new ArgumentException($"Invalid branch OpCode: {branchOpCode}");

            if (c.Next == null || !c.Next.MatchCallOrCallvirt(out MethodReference nextMethodCall))
            {
                Log.Error($"Failed to find method call to skip: {c.Context.Method.FullName} at instruction {c.Next.SafeToString()} ({c.Index})");
                return;
            }

            MethodDefinition method = nextMethodCall.SafeResolve();

            if (method == null)
            {
                Log.Error($"Failed to resolve method '{nextMethodCall.FullName}': {c.Context.Method.FullName} at instruction {c.Next.SafeToString()} ({c.Index})");
                return;
            }

            int parameterCount = method.Parameters.Count + (!method.IsStatic ? 1 : 0);
            bool isVoidReturn = method.ReturnType.Is(typeof(void));

            ILLabel skipCallLabel = c.DefineLabel();

            if (branchOpCode.FlowControl == FlowControl.Branch)
            {
                c.Emit(OpCodes.Ldc_I4_0);
                c.Emit(OpCodes.Brfalse, skipCallLabel);
            }
            else
            {
                c.Emit(branchOpCode, skipCallLabel);
            }

            c.Index++;

            if (parameterCount == 0 && isVoidReturn)
            {
                c.MarkLabel(skipCallLabel);
                return;
            }

            ILLabel afterPatchLabel = c.DefineLabel();
            c.Emit(OpCodes.Br, afterPatchLabel);

            c.MarkLabel(skipCallLabel);

            for (int i = 0; i < parameterCount; i++)
            {
                c.Emit(OpCodes.Pop);
            }

            if (emitSkippedReturnValue != null)
            {
                emitSkippedReturnValue(c);
            }
            else if (!isVoidReturn)
            {
                Log.Warning($"Skipped method ({method.FullName}) is not void, emitting default value: {c.Context.Method.FullName} at instruction {c.Next.SafeToString()} ({c.Index})");

                if (method.ReturnType.IsValueType)
                {
                    VariableDefinition tmpVar = c.Context.AddVariable(method.ReturnType);

                    c.Emit(OpCodes.Ldloca, tmpVar);
                    c.Emit(OpCodes.Initobj, method.ReturnType);

                    c.Emit(OpCodes.Ldloc, tmpVar);
                }
                else
                {
                    c.Emit(OpCodes.Ldnull);
                }
            }

            c.MarkLabel(afterPatchLabel);
        }

        /// <summary>
        /// Finds the instruction that a <see langword="continue"/> statement would jump to.
        /// </summary>
        /// <param name="cursor">The cursor to search from</param>
        /// <param name="continueLabel">A label that represents the destination of the <see langword="continue"/> keyword</param>
        /// <remarks>
        /// Does not move the cursor or emit instructions.
        /// <para/>
        /// Must be run from inside a foreach block (the closest GetEnumerator call going backwards is considered the current loop)
        /// </remarks>
        /// <returns><see langword="true"/> if a <see langword="continue"/> label is found, otherwise <see langword="false"/></returns>
        public static bool TryFindForeachContinueLabel(this ILCursor cursor, out ILLabel continueLabel)
        {
            static bool isEnumerableGetEnumerator(MethodReference method)
            {
                if (method == null)
                    return false;

                if (!string.Equals(method.Name, nameof(IEnumerable.GetEnumerator)))
                    return false;

                // TODO: More robust check here, check return type is an IEnumerator and make sure declaring type is actually implementing the IEnumerable interface

                return true;
            }

            ILCursor c = cursor.Clone();

            int enumeratorLocalIndex = -1;
            if (!c.TryGotoPrev(x => x.MatchCallOrCallvirt(out MethodReference method) && isEnumerableGetEnumerator(method)) ||
                !c.TryGotoNext(x => x.MatchStloc(out enumeratorLocalIndex)))
            {
                Log.Warning("Failed to find GetEnumerator call");
                continueLabel = null;
                return false;
            }

            c = cursor.Clone();
            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchLdloc(enumeratorLocalIndex),
                               x => x.MatchCallOrCallvirt<IEnumerator>(nameof(IEnumerator.MoveNext))))
            {
                Log.Warning("Failed to find matching MoveNext call");
                continueLabel = null;
                return false;
            }

            continueLabel = c.MarkLabel();
            return true;
        }

        /// <summary>
        /// Converts the <paramref name="instruction"/> into a string representation, handling some edge cases that the standard <see cref="Instruction.ToString"/> does not.
        /// </summary>
        /// <param name="instruction">The instruction to convert</param>
        /// <returns>A string that represents the <paramref name="instruction"/></returns>
        public static string SafeToString(this Instruction instruction)
        {
            if (instruction == null)
                return "NULL";

            try
            {
                return instruction.ToString();
            }
            catch
            {
            }

            static string formatLabel(Instruction instruction)
            {
                if (instruction == null)
                {
                    return "IL_????";
                }

                return $"IL_{instruction.Offset:x4}";
            }

            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.Append(formatLabel(instruction))
                         .Append(": ")
                         .Append(instruction.OpCode.Name);

            if (instruction.Operand != null)
            {
                stringBuilder.Append(' ');

                switch (instruction.Operand)
                {
                    case Instruction instructionOperand:
                        stringBuilder.Append(formatLabel(instructionOperand));
                        break;
                    case IEnumerable<Instruction> instructionsOperand:

                        stringBuilder.Append('[')
                                     .Append(string.Join(", ", instructionsOperand.Select(formatLabel)))
                                     .Append(']');

                        break;
                    default:
                        stringBuilder.Append(instruction.Operand);
                        break;
                }
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Searches a method for any matching parameters.
        /// </summary>
        /// <param name="method">The method to search</param>
        /// <param name="parameterType">The type of the parameter to find</param>
        /// <param name="parameterName">The name of the parameter to find</param>
        /// <param name="parameter">The matching parameter, if found</param>
        /// <remarks>
        /// <paramref name="parameterType"/> and <paramref name="parameterName"/> can be <see langword="null"/> to indicate a wildcard in its place.
        /// <para/>
        /// Ex. <paramref name="parameterType"/> = <see langword="int"/> and <paramref name="parameterName"/> = "num" will find an <see langword="int"/> parameter named "num"
        /// <para/>
        /// Ex. <paramref name="parameterType"/> = <see langword="int"/> and <paramref name="parameterName"/> = <see langword="null"/> will find any <see langword="int"/> parameter, regardless of name
        /// <para/>
        /// Ex. <paramref name="parameterType"/> = <see langword="null"/> and <paramref name="parameterName"/> = "num" will find any parameter named "num", regardless of type
        /// <para/>
        /// At least one of <paramref name="parameterType"/> and <paramref name="parameterName"/> must be non-null
        /// </remarks>
        /// <returns><see langword="true"/> if a matching parameter is found, otherwise <see langword="false"/></returns>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/></exception>
        /// <exception cref="ArgumentException">Both <paramref name="parameterType"/> and <paramref name="parameterName"/> are <see langword="null"/></exception>
        public static bool TryFindParameter(this MethodReference method, Type parameterType, string parameterName, out ParameterDefinition parameter)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));

            if (parameterType == null && string.IsNullOrEmpty(parameterName))
                throw new ArgumentException($"Cannot find parameter for method {method.FullName}: Neither parameter type or name specified");

            if (method.HasParameters)
            {
                foreach (ParameterDefinition param in method.Parameters)
                {
                    if ((string.IsNullOrEmpty(parameterName) || param.Name == parameterName) && (parameterType == null || param.ParameterType.Is(parameterType)))
                    {
                        parameter = param;
                        return true;
                    }
                }
            }

            parameter = null;
            return false;
        }

        /// <summary>
        /// Searches a method for any parameters matching <paramref name="name"/>.
        /// </summary>
        /// <param name="method">The method to search</param>
        /// <param name="name">The name of the parameter to match</param>
        /// <param name="parameter">The matching parameter, if found</param>
        /// <returns><see langword="true"/> if a matching parameter is found, otherwise <see langword="false"/></returns>
        public static bool TryFindParameter(this MethodReference method, string name, out ParameterDefinition parameter)
        {
            return TryFindParameter(method, null, name, out parameter);
        }

        /// <summary>
        /// Searches a method for any parameters matching <paramref name="type"/>.
        /// </summary>
        /// <param name="method">The method to search</param>
        /// <param name="type">The type of the parameter to match</param>
        /// <param name="parameter">The matching parameter, if found</param>
        /// <returns><see langword="true"/> if a matching parameter is found, otherwise <see langword="false"/></returns>
        public static bool TryFindParameter(this MethodReference method, Type type, out ParameterDefinition parameter)
        {
            return TryFindParameter(method, type, null, out parameter);
        }

        /// <summary>
        /// Searches a method for any parameters matching type <typeparamref name="T"/> and <paramref name="name"/>.
        /// </summary>
        /// <typeparam name="T">The type of the parameter to match</typeparam>
        /// <param name="method">The method to search</param>
        /// <param name="name">The name of the parameter to match, can be <see langword="null"/> to match any parameter of type <typeparamref name="T"/></param>
        /// <param name="parameter">The matching parameter, if found</param>
        /// <returns><see langword="true"/> if a matching parameter is found, otherwise <see langword="false"/></returns>
        public static bool TryFindParameter<T>(this MethodReference method, string name, out ParameterDefinition parameter)
        {
            return TryFindParameter(method, typeof(T), name, out parameter);
        }

        /// <summary>
        /// Searches a method for any parameters matching type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the parameter to match</typeparam>
        /// <param name="method">The method to search</param>
        /// <param name="parameter">The matching parameter, if found</param>
        /// <returns><see langword="true"/> if a matching parameter is found, otherwise <see langword="false"/></returns>
        public static bool TryFindParameter<T>(this MethodReference method, out ParameterDefinition parameter)
        {
            return TryFindParameter(method, typeof(T), null, out parameter);
        }

        /// <summary>
        /// Adds and returns a variable of type <paramref name="variableType"/>
        /// </summary>
        /// <param name="context"></param>
        /// <param name="variableType">The type of the variable to create</param>
        /// <returns>The added variable</returns>
        public static VariableDefinition AddVariable(this ILContext context, TypeReference variableType)
        {
            VariableDefinition variableDefinition = new VariableDefinition(variableType);
            context.Method.Body.Variables.Add(variableDefinition);
            return variableDefinition;
        }

        /// <summary>
        /// Adds and returns a variable of type <paramref name="variableType"/>
        /// </summary>
        /// <param name="context"></param>
        /// <param name="variableType">The type of the variable to create</param>
        /// <returns>The added variable</returns>
        public static VariableDefinition AddVariable(this ILContext context, Type variableType)
        {
            return AddVariable(context, context.Import(variableType));
        }

        /// <summary>
        /// Adds and returns a variable of type <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T">The type of the variable to create</typeparam>
        /// <param name="context"></param>
        /// <returns>The added variable</returns>
        public static VariableDefinition AddVariable<T>(this ILContext context)
        {
            return AddVariable(context, context.Import(typeof(T)));
        }

        /// <summary>
        /// Stores all values on the stack in the variables represented by the <paramref name="variables"/> parameter
        /// </summary>
        /// <param name="cursor"></param>
        /// <param name="variables">The variables to store the stack's values in, defined in the order the values should be pushed back onto the stack</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void EmitStoreStack(this ILCursor cursor, params VariableDefinition[] variables)
        {
            if (cursor is null)
                throw new ArgumentNullException(nameof(cursor));

            if (variables is null)
                throw new ArgumentNullException(nameof(variables));

            if (variables.Length == 0)
                return;

            for (int i = variables.Length - 1; i >= 1; i--)
            {
                cursor.Emit(OpCodes.Stloc, variables[i]);
            }

            cursor.Emit(OpCodes.Dup);
            cursor.Emit(OpCodes.Stloc, variables[0]);

            for (int i = 1; i < variables.Length; i++)
            {
                cursor.Emit(OpCodes.Ldloc, variables[i]);
            }
        }
    }
}
