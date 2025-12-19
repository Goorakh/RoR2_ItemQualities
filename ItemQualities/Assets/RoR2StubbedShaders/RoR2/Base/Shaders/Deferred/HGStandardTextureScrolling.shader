Shader "StubbedRoR2/Base/Shaders/HGStandardTextureScrolling" {
	Properties {
		[Header(Rendering Settings)] [MaterialEnum(Off,0,Front,1,Back,2)] _Cull ("Cull", Float) = 2
		[Toggle(CUTOUT)] _EnableCutout ("Enable Cutout", Float) = 0
		[MaterialEnum(Default,0,Environment,1,Character,2,Misc,3)] _DecalLayer ("Decal Layer", Float) = 0
		[MaterialEnum(Two Tone,0,Smoothed Two Tone,1,Unlitish,3,Subsurface,4,Grass,5)] _RampInfo ("Ramp Choice", Float) = 0
		[Header(Main Textures)] _Color ("Main Color", Color) = (0.5,0.5,0.5,1)
		_BaseTex ("Base Texture (RGB)", 2D) = "white" {}
		_BaseTex_TilingOffset ("Base Texture Tiling and Offset", Vector) = (1,1,0,0)
		[NoScaleOffset] _BaseTexScrollSpeed ("Base Texture Scroll Speed (XY)", Vector) = (0,0,0,0)
		[Space(10)] _NormalTex ("Normal Map", 2D) = "bump" {}
		_NormalStrength ("Normal Strength", Range(0, 5)) = 1
		[Header(Surface Properties)] _Smoothness ("Smoothness", Range(0, 1)) = 0
		_SecondarySmoothness ("Smoothness for Vertex Color Override", Range(0, 1)) = 0
		_SpecularStrength ("Specular Strength", Range(0, 1)) = 0
		_SpecularExponent ("Specular Exponent", Range(0.1, 20)) = 1
		[Toggle(FORCE_SPEC)] _ForceSpecOn ("Ignore Diffuse Alpha for Speculars", Float) = 0
		[Header(Emission Settings)] _EmColor ("Emission Color", Color) = (0,0,0,1)
		[NoScaleOffset] _EmTex ("Emission Texture (RGB)", 2D) = "white" {}
		_EmPower ("Emission Power", Range(0, 10)) = 1
		_EmBoost ("Emission Saturation", Range(0, 100)) = 1
		[Header(Scrolling Textures)] _ScrollTex ("Scroll Texture (RGB)", 2D) = "white" {}
		_TexScrollSpeed ("Emission Texture Scroll Speed (XY)", Vector) = (0.2,0.2,0,0)
		[Space(10)] _InnerColor ("Inner Color", Color) = (0.1,0.1,0.1,1)
		_InnerColorIntensity ("Inner Color Intensity", Range(0, 1)) = 0.2
		[Header(Vertical Color Gradient)] [Toggle(FLIPVERTICAL)] _flipVerticalMode ("Flip Gradient Direction", Float) = 0
		_ModelHeight ("Model Height", Float) = 1
		_VertColor ("Vertical Effect Color", Color) = (0.5,0.5,0.5,1)
		_GradientCenter ("Gradient Center Position", Range(-4, 4)) = 0
		_GradientWidth ("Transition Zone Width", Range(0.1, 5)) = 1
		_GradientSmoothness ("Gradient Smoothness", Range(0.1, 3)) = 1
		[Header(Fresnel Emission)] [Toggle(FRESNEL_EMISSION)] _FEON ("Enable Fresnel Emission", Float) = 0
		[Toggle(USE_VERTEX_COLORS_FOR_FRESNEL_EMISSION_AND_FLOWMAP)] _DoubleColorsOn ("Use Vertex Colors as mask for Fresnel mask and flowmap", Float) = 0
		[NoScaleOffset] _FresnelRamp ("Fresnel Ramp", 2D) = "white" {}
		_FresnelPower ("Fresnel Power", Range(0.1, 20)) = 1
		[NoScaleOffset] _FresnelMask ("Fresnel Mask", 2D) = "white" {}
		_FresnelBoost ("Fresnel Boost", Range(0, 20)) = 1
		[Header(Print Behavior)] [Toggle(PRINT_CUTOFF)] _PrintOn ("Enable Printing", Float) = 0
		[NoScaleOffset] _PrintRamp ("Print Ramp", 2D) = "gray" {}
		[Space(5)] _SliceHeight ("Slice Height", Range(-25, 25)) = 5
		_SliceBandHeight ("Print Band Height", Range(0, 200)) = 1
		_SliceAlphaDepth ("Print Alpha Depth", Range(0, 1)) = 0.1
		_SliceAlphaTex ("Print Alpha Texture", 2D) = "gray" {}
		[Space(5)] _PrintBoost ("Print Color Boost", Range(0, 10)) = 1
		_PrintBias ("Print Alpha Bias", Range(0, 4)) = 0
		_PrintEmissionToAlbedoLerp ("Print Emission to Albedo Lerp", Range(0, 1)) = 0
		[MaterialEnum(BottomUp,0,TopDown,1,BackToFront,3)] _PrintDirection ("Print Direction", Float) = 0
		[Header(Flowmap Effects)] [Toggle(FLOWMAP)] _FlowmapOn ("Enable Flowmap", Float) = 0
		[NoScaleOffset] _FlowTex ("Flow Vector (RG), Noise (B)", 2D) = "bump" {}
		_FlowHeightmap ("Flow Heightmap", 2D) = "white" {}
		_FlowHeightRamp ("Flow Ramp", 2D) = "black" {}
		[Space(5)] _FlowSpeed ("Flow Speed", Range(0, 15)) = 1
		_FlowTextureScaleFactor ("Flow Texture Scale Factor", Range(0, 10)) = 1
		[Space(5)] _FlowHeightBias ("Flow Height Bias", Range(-1, 1)) = 0
		_FlowHeightPower ("Flow Height Power", Range(0.1, 20)) = 1
		_FlowEmissionStrength ("Flow Height Strength", Range(0.1, 20)) = 1
		_FlowMaskStrength ("Mask Flow Strength", Range(0, 5)) = 0
		_FlowNormalStrength ("Normal Flow Strength", Range(0, 5)) = 1
		[Header(Elite Remap Behavior)] [PerRendererData] _EliteIndex ("Elite Index", Float) = 0
		_EliteBrightnessMin ("Elite Brightness, Min", Range(-10, 10)) = 0
		_EliteBrightnessMax ("Elite Brightness, Max", Range(-10, 10)) = 1
		[Header(Limb Removal)] [Toggle(LIMBREMOVAL)] _LimbRemovalOn ("Enable Limb Removal", Float) = 0
		_LimbPrimeMask ("Limb Prime Mask", Range(1, 10000)) = 1
		_FlashColor ("Flash Color", Color) = (0,0,0,1)
		[PerRendererData] _Fade ("Fade", Range(0, 1)) = 1
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200
		CGPROGRAM
#pragma surface surf Standard
#pragma target 3.0

		fixed4 _Color;
		struct Input
		{
			float2 uv_MainTex;
		};
		
		void surf(Input IN, inout SurfaceOutputStandard o)
		{
			o.Albedo = _Color.rgb;
			o.Alpha = _Color.a;
		}
		ENDCG
	}
	Fallback "Diffuse"
}