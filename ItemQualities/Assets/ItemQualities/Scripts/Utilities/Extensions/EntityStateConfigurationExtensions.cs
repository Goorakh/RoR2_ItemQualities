using HG.GeneralSerializer;
using RoR2;
using System;

namespace ItemQualities.Utilities.Extensions
{
    static class EntityStateConfigurationExtensions
    {
        public static bool TryGetFieldValue<T>(this EntityStateConfiguration entityStateConfiguration, string fieldName, out T value)
        {
            foreach (SerializedField field in entityStateConfiguration.serializedFieldsCollection.serializedFields)
            {
                if (field.fieldName == fieldName)
                {
                    bool isUnityObjectValue = typeof(UnityEngine.Object).IsAssignableFrom(typeof(T));
                    if (isUnityObjectValue)
                    {
                        if (!field.fieldValue.objectValue)
                        {
                            value = default;
                            return true;
                        }
                        else if (field.fieldValue.objectValue is T objectValue)
                        {
                            value = objectValue;
                            return true;
                        }
                    }
                    else
                    {
                        try
                        {
                            value = (T)StringSerializer.Deserialize(typeof(T), field.fieldValue.stringValue);
                            return true;
                        }
                        catch (Exception e)
                        {
                            Log.Error($"Failed to deserialize field value '{field.fieldValue.stringValue}' for field {field.fieldName} in {entityStateConfiguration}: {e}");
                        }
                    }

                    break;
                }
            }

            value = default;
            return false;
        }

        public static bool TrySetFieldValue<T>(this EntityStateConfiguration entityStateConfiguration, string fieldName, T value)
        {
            ref SerializedFieldCollection fieldsCollection = ref entityStateConfiguration.serializedFieldsCollection;
            for (int i = 0; i < fieldsCollection.serializedFields.Length; i++)
            {
                ref SerializedField field = ref fieldsCollection.serializedFields[i];
                if (field.fieldName == fieldName)
                {
                    bool isUnityObjectValue = typeof(UnityEngine.Object).IsAssignableFrom(typeof(T));

                    if (isUnityObjectValue)
                    {
                        field.fieldValue.objectValue = value as UnityEngine.Object;
                        return true;
                    }
                    else
                    {
                        try
                        {
                            field.fieldValue.stringValue = StringSerializer.Serialize(typeof(T), value);
                            return true;
                        }
                        catch (Exception e)
                        {
                            Log.Error($"Failed to serialize field value '{value}' for field {field.fieldName} in {entityStateConfiguration}: {e}");
                        }
                    }

                    break;
                }
            }

            return false;
        }
    }
}
