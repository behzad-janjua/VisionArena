Shader "Unreal/MI_OceanWater"
{
	Properties
	{
		_MainTex("MainTex (RGB)", 2D) = "white" {}
		Material_Texture2D_0( "Normal", 2D ) = "white" {}
		Material_Texture2D_1( "T_NM", 2D ) = "white" {}
		Material_Texture2D_2( "T_flowmapPaintForTile_NM", 2D ) = "white" {}
		Material_Texture2D_3( "Foam01", 2D ) = "white" {}
		Material_Texture2D_4( "Foam02", 2D ) = "white" {}
		Material_Texture2D_5( "Foam03", 2D ) = "white" {}
		Material_Texture2D_6( "height", 2D ) = "white" {}

		View_BufferSizeAndInvSize( "View_BufferSizeAndInvSize", Vector ) = ( 1920,1080,0.00052, 0.00092 )//1920,1080,1/1920, 1/1080
		LocalObjectBoundsMin( "LocalObjectBoundsMin", Vector ) = ( 0, 0, 0, 0 )
		LocalObjectBoundsMax( "LocalObjectBoundsMax", Vector ) = ( 100, 100, 100, 0 )
	}
	SubShader 
	{
		//BLEND_OFF Tags { "RenderType" = "Opaque" }
		 Tags { "RenderType" = "Transparent"  "Queue" = "Transparent" }
		
		//Blend SrcAlpha OneMinusSrcAlpha
		Cull Off

		CGPROGRAM

		#include "UnityPBSLighting.cginc"
		//BLEND_OFF #pragma surface surf Standard vertex:vert addshadow
		 #pragma surface surf Standard vertex:vert alpha:fade addshadow
		
		//#pragma target 3.0 //WebGL seems to work with 3.0, 2.0 needs modifications
		#pragma target 5.0

		#define NUM_TEX_COORD_INTERPOLATORS 1
		#define NUM_MATERIAL_TEXCOORDS_VERTEX 1
		#define NUM_CUSTOM_VERTEX_INTERPOLATORS 0

		struct Input
		{
			//float3 Normal;
			float2 uv_MainTex : TEXCOORD0;
			float2 uv2_Material_Texture2D_0 : TEXCOORD1;
			//float2 uv2_MainTex : TEXCOORD1;
			float4 color : COLOR;
			float4 tangent;
			//float4 normal;
			float3 viewDir;
			float4 screenPos;
			float3 worldPos;
			//float3 worldNormal;
			float3 normal2;
			INTERNAL_DATA
		};
		void vert( inout appdata_full i, out Input o )
		{
			float3 p_normal = mul( float4( i.normal, 0.0f ), unity_WorldToObject );
			//half4 p_tangent = mul( unity_ObjectToWorld,i.tangent );

			//half3 normal_input = normalize( p_normal.xyz );
			//half3 tangent_input = normalize( p_tangent.xyz );
			//half3 binormal_input = cross( p_normal.xyz,tangent_input.xyz ) * i.tangent.w;
			UNITY_INITIALIZE_OUTPUT( Input, o );

			//o.worldNormal = p_normal;
			o.normal2 = p_normal;
			o.tangent = i.tangent;
			//o.binormal_input = binormal_input;
		}
		uniform sampler2D _MainTex;
		/*
		struct SurfaceOutputStandard
		{
		fixed3 Albedo;		// base (diffuse or specular) color
		fixed3 Normal;		// tangent space normal, if written
		half3 Emission;
		half Metallic;		// 0=non-metal, 1=metal
		// Smoothness is the user facing name, it should be perceptual smoothness but user should not have to deal with it.
		// Everywhere in the code you meet smoothness it is perceptual smoothness
		half Smoothness;	// 0=rough, 1=smooth
		half Occlusion;		// occlusion (default 1)
		fixed Alpha;		// alpha for transparencies
		};
		*/


		#define Texture2D sampler2D
		#define TextureCube samplerCUBE
		#define SamplerState int

		#define UE5
		#define MATERIAL_TANGENTSPACENORMAL 1
		//struct Material
		//{
			//samplers start
			uniform sampler2D    Material_Texture2D_0;
			uniform SamplerState Material_Texture2D_0Sampler;
			uniform sampler2D    Material_Texture2D_1;
			uniform SamplerState Material_Texture2D_1Sampler;
			uniform sampler2D    Material_Texture2D_2;
			uniform SamplerState Material_Texture2D_2Sampler;
			uniform sampler2D    Material_Texture2D_3;
			uniform SamplerState Material_Texture2D_3Sampler;
			uniform sampler2D    Material_Texture2D_4;
			uniform SamplerState Material_Texture2D_4Sampler;
			uniform sampler2D    Material_Texture2D_5;
			uniform SamplerState Material_Texture2D_5Sampler;
			uniform sampler2D    Material_Texture2D_6;
			uniform SamplerState Material_Texture2D_6Sampler;
			
		//};

		#ifdef UE5
			#define UE_LWC_RENDER_TILE_SIZE			2097152.0
			#define UE_LWC_RENDER_TILE_SIZE_SQRT	1448.15466
			#define UE_LWC_RENDER_TILE_SIZE_RSQRT	0.000690533954
			#define UE_LWC_RENDER_TILE_SIZE_RCP		4.76837158e-07
			#define UE_LWC_RENDER_TILE_SIZE_FMOD_PI		0.673652053
			#define UE_LWC_RENDER_TILE_SIZE_FMOD_2PI	0.673652053
			#define INVARIANT(X) X
			#define PI 					(3.1415926535897932)

			#include "LargeWorldCoordinates.hlsl"
		#endif
		struct MaterialStruct
		{
			float4 PreshaderBuffer[72];
			float4 ScalarExpressions[1];
			float VTPackedPageTableUniform[2];
			float VTPackedUniform[1];
		};
		struct ViewStruct
		{
			float GameTime;
			float RealTime;
			float DeltaTime;
			float PrevFrameGameTime;
			float PrevFrameRealTime;
			float MaterialTextureMipBias;
			SamplerState MaterialTextureBilinearWrapedSampler;
			SamplerState MaterialTextureBilinearClampedSampler;
			float4 PrimitiveSceneData[ 40 ];
			float4 TemporalAAParams;
			float2 ViewRectMin;
			float4 ViewSizeAndInvSize;
			float MaterialTextureDerivativeMultiply;
			uint StateFrameIndexMod8;
			float FrameNumber;
			float2 FieldOfViewWideAngles;
			float4 RuntimeVirtualTextureMipLevel;
			float PreExposure;
			float4 BufferBilinearUVMinMax;
		};
		struct ResolvedViewStruct
		{
		#ifdef UE5
			FLWCVector3 WorldCameraOrigin;
			FLWCVector3 PrevWorldCameraOrigin;
			FLWCVector3 PreViewTranslation;
			FLWCVector3 WorldViewOrigin;
		#else
			float3 WorldCameraOrigin;
			float3 PrevWorldCameraOrigin;
			float3 PreViewTranslation;
			float3 WorldViewOrigin;
		#endif
			float4 ScreenPositionScaleBias;
			float4x4 TranslatedWorldToView;
			float4x4 TranslatedWorldToCameraView;
			float4x4 TranslatedWorldToClip;
			float4x4 ViewToTranslatedWorld;
			float4x4 PrevViewToTranslatedWorld;
			float4x4 CameraViewToTranslatedWorld;
			float4 BufferBilinearUVMinMax;
			float4 XRPassthroughCameraUVs[ 2 ];
		};
		struct PrimitiveStruct
		{
			float4x4 WorldToLocal;
			float4x4 LocalToWorld;
		};

		ViewStruct View;
		ResolvedViewStruct ResolvedView;
		PrimitiveStruct Primitive;
		uniform float4 View_BufferSizeAndInvSize;
		uniform float4 LocalObjectBoundsMin;
		uniform float4 LocalObjectBoundsMax;
		uniform SamplerState Material_Wrap_WorldGroupSettings;
		uniform SamplerState Material_Clamp_WorldGroupSettings;
		
		#define PI UNITY_PI
		#include "UnrealCommon.cginc"

		MaterialStruct Material;
void InitializeExpressions()
{
	Material.PreshaderBuffer[0] = float4(0.000000,0.000000,0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[1] = float4(1.000000,0.002000,0.800000,0.100000);//(Unknown)
	Material.PreshaderBuffer[2] = float4(0.100000,10.000000,0.200000,60.000000);//(Unknown)
	Material.PreshaderBuffer[3] = float4(1.000000,1.500000,0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[4] = float4(0.000000,0.000000,0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[5] = float4(0.053704,0.208333,0.195884,1.000000);//(Unknown)
	Material.PreshaderBuffer[6] = float4(0.000480,0.021762,0.041667,1.000000);//(Unknown)
	Material.PreshaderBuffer[7] = float4(194.695938,194.695938,0.005136,0.000000);//(Unknown)
	Material.PreshaderBuffer[8] = float4(0.000480,0.021762,0.041667,0.000000);//(Unknown)
	Material.PreshaderBuffer[9] = float4(0.053704,0.208333,0.195884,0.000000);//(Unknown)
	Material.PreshaderBuffer[10] = float4(0.144358,0.371209,0.421875,1.000000);//(Unknown)
	Material.PreshaderBuffer[11] = float4(0.144358,0.371209,0.421875,0.100000);//(Unknown)
	Material.PreshaderBuffer[12] = float4(0.600000,3.000000,0.150000,0.003750);//(Unknown)
	Material.PreshaderBuffer[13] = float4(0.023562,0.023560,-0.023560,0.999722);//(Unknown)
	Material.PreshaderBuffer[14] = float4(0.999722,-0.023560,0.023560,0.999722);//(Unknown)
	Material.PreshaderBuffer[15] = float4(0.457143,0.457143,0.100000,-0.030000);//(Unknown)
	Material.PreshaderBuffer[16] = float4(3.000000,-32.644753,-0.816119,-5.127826);//(Unknown)
	Material.PreshaderBuffer[17] = float4(0.914940,-0.914940,0.403590,0.000000);//(Unknown)
	Material.PreshaderBuffer[18] = float4(0.403590,-0.914940,0.914940,0.403590);//(Unknown)
	Material.PreshaderBuffer[19] = float4(0.400000,0.500000,0.100000,0.050000);//(Unknown)
	Material.PreshaderBuffer[20] = float4(8.000000,116.000000,2.900000,18.221239);//(Unknown)
	Material.PreshaderBuffer[21] = float4(-0.587784,0.587784,0.809018,0.000000);//(Unknown)
	Material.PreshaderBuffer[22] = float4(0.809018,0.587784,-0.587784,0.809018);//(Unknown)
	Material.PreshaderBuffer[23] = float4(0.400000,0.500000,0.100000,1.832323);//(Unknown)
	Material.PreshaderBuffer[24] = float4(0.200000,0.010000,125.939491,125.939491);//(Unknown)
	Material.PreshaderBuffer[25] = float4(0.007940,0.980000,5.000000,1.330000);//(Unknown)
	Material.PreshaderBuffer[26] = float4(0.000000,0.000000,0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[27] = float4(-753913856.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[28] = float4(-754221056.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[29] = float4(-751456256.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[30] = float4(-752910336.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[31] = float4(-752910336.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[32] = float4(-752910336.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[33] = float4(-754507776.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[34] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[35] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[36] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[37] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[38] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[39] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[40] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[41] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[42] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[43] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[44] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[45] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[46] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[47] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[48] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[49] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[50] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[51] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[52] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[53] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[54] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[55] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[56] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[57] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[58] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[59] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[60] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[61] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[62] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[63] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[64] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[65] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[66] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[67] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[68] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[69] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[70] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
	Material.PreshaderBuffer[71] = float4(-0.000000,0.000000,-0.000000,0.000000);//(Unknown)
}void CalcPixelMaterialInputs(in out FMaterialPixelParameters Parameters, in out FPixelMaterialInputs PixelMaterialInputs)
{
	//WorldAligned texturing & others use normals & stuff that think Z is up
	Parameters.TangentToWorld[0] = Parameters.TangentToWorld[0].xzy;
	Parameters.TangentToWorld[1] = Parameters.TangentToWorld[1].xzy;
	Parameters.TangentToWorld[2] = Parameters.TangentToWorld[2].xzy;

	float3 WorldNormalCopy = Parameters.WorldNormal;

	// Initial calculations (required for Normal)
	MaterialFloat Local0 = (View.GameTime * Material.PreshaderBuffer[1].x);
	MaterialFloat Local1 = (Local0 * MaterialFloat2(17.50000000,8.50000000).r);
	MaterialFloat Local2 = (Local0 * MaterialFloat2(17.50000000,8.50000000).g);
	FLWCVector3 Local3 = GetWorldPosition(Parameters);
	FLWCVector2 Local4 = MakeLWCVector(LWCGetX(DERIV_BASE_VALUE(Local3)), LWCGetY(DERIV_BASE_VALUE(Local3)));
	FLWCVector2 Local5 = LWCAdd(LWCPromote(MaterialFloat2(Local1,Local2)), DERIV_BASE_VALUE(Local4));
	FLWCVector2 Local6 = LWCMultiply(DERIV_BASE_VALUE(Local5), LWCPromote(((MaterialFloat2)Material.PreshaderBuffer[1].y)));
	MaterialFloat2 Local7 = LWCApplyAddressMode(DERIV_BASE_VALUE(Local6), LWCADDRESSMODE_WRAP, LWCADDRESSMODE_WRAP);
	MaterialFloat Local8 = MaterialStoreTexCoordScale(Parameters, Local7, 3);
	MaterialFloat4 Local9 = UnpackNormalMap(Texture2DSampleBias(Material_Texture2D_0,Material_Texture2D_0Sampler,Local7,View.MaterialTextureMipBias));
	MaterialFloat Local10 = MaterialStoreTexSample(Parameters, Local9, 3);
	MaterialFloat Local11 = (Local0 * MaterialFloat2(0.25000000,-15.00000000).r);
	MaterialFloat Local12 = (Local0 * MaterialFloat2(0.25000000,-15.00000000).g);
	FLWCVector2 Local13 = LWCAdd(LWCPromote(MaterialFloat2(Local11,Local12)), DERIV_BASE_VALUE(Local4));
	FLWCVector2 Local14 = LWCMultiply(DERIV_BASE_VALUE(Local13), LWCPromote(((MaterialFloat2)Material.PreshaderBuffer[1].y)));
	MaterialFloat2 Local15 = LWCApplyAddressMode(DERIV_BASE_VALUE(Local14), LWCADDRESSMODE_WRAP, LWCADDRESSMODE_WRAP);
	MaterialFloat Local16 = MaterialStoreTexCoordScale(Parameters, Local15, 3);
	MaterialFloat4 Local17 = UnpackNormalMap(Texture2DSampleBias(Material_Texture2D_0,Material_Texture2D_0Sampler,Local15,View.MaterialTextureMipBias));
	MaterialFloat Local18 = MaterialStoreTexSample(Parameters, Local17, 3);
	MaterialFloat2 Local19 = (MaterialFloat2(Local9.r,Local9.g) + MaterialFloat2(Local17.r,Local17.g));
	MaterialFloat Local20 = (Local0 * MaterialFloat2(2.15000010,2.79999995).r);
	MaterialFloat Local21 = (Local0 * MaterialFloat2(2.15000010,2.79999995).g);
	FLWCVector2 Local22 = LWCAdd(LWCPromote(MaterialFloat2(Local20,Local21)), DERIV_BASE_VALUE(Local4));
	FLWCVector2 Local23 = LWCMultiply(DERIV_BASE_VALUE(Local22), LWCPromote(MaterialFloat2(0.00010000,0.00040000)));
	MaterialFloat2 Local24 = LWCApplyAddressMode(DERIV_BASE_VALUE(Local23), LWCADDRESSMODE_WRAP, LWCADDRESSMODE_WRAP);
	MaterialFloat Local25 = MaterialStoreTexCoordScale(Parameters, Local24, 3);
	MaterialFloat4 Local26 = UnpackNormalMap(Texture2DSampleBias(Material_Texture2D_0,Material_Texture2D_0Sampler,Local24,View.MaterialTextureMipBias));
	MaterialFloat Local27 = MaterialStoreTexSample(Parameters, Local26, 3);
	MaterialFloat2 Local28 = (MaterialFloat2(Local26.r,Local26.g) * ((MaterialFloat2)0.20000000));
	MaterialFloat2 Local29 = (Local19 + Local28);
	MaterialFloat2 Local30 = (Local29 * Local29);
	MaterialFloat Local31 = (1.00000000 - Local30.r);
	MaterialFloat Local32 = (Local31 - Local30.g);
	MaterialFloat Local33 = sqrt(Local32);
	MaterialFloat Local34 = (Local33 * 1.00000000);
	MaterialFloat Local35 = dot(MaterialFloat3(Local29,Local34),MaterialFloat3(Local29,Local34));
	MaterialFloat3 Local36 = normalize(MaterialFloat3(Local29,Local34));
	MaterialFloat4 Local37 = ((abs(Local35 - 0.00000100) > 0.00001000) ? ((Local35 >= 0.00000100) ? MaterialFloat4(Local36,0.00000000) : MaterialFloat4(MaterialFloat3(0.00000000,0.00000000,1.00000000),1.00000000)) : MaterialFloat4(MaterialFloat3(0.00000000,0.00000000,1.00000000),1.00000000));
	MaterialFloat2 Local38 = Parameters.TexCoords[0].xy;
	MaterialFloat Local39 = MaterialStoreTexCoordScale(Parameters, DERIV_BASE_VALUE(Local38), 7);
	MaterialFloat4 Local40 = UnpackNormalMap(Texture2DSampleBias(Material_Texture2D_1,Material_Texture2D_1Sampler,DERIV_BASE_VALUE(Local38),View.MaterialTextureMipBias));
	MaterialFloat Local41 = MaterialStoreTexSample(Parameters, Local40, 7);
	MaterialFloat3 Local42 = lerp(Local37.rgb,Local40.rgb,Material.PreshaderBuffer[1].z);
	MaterialFloat3 Local43 = LWCDdy(DERIV_BASE_VALUE(Local3));
	MaterialFloat3 Local44 = cross(DERIV_BASE_VALUE(Local43),Parameters.TangentToWorld[2]);
	MaterialFloat3 Local45 = LWCDdx(DERIV_BASE_VALUE(Local3));
	MaterialFloat Local46 = dot(Local44,DERIV_BASE_VALUE(Local45));
	MaterialFloat Local47 = abs(Local46);
	MaterialFloat3 Local48 = (((MaterialFloat3)Local47) * Parameters.TangentToWorld[2]);
	MaterialFloat Local49 = (View.GameTime * Material.PreshaderBuffer[1].w);
	MaterialFloat Local50 = (Local49 * 0.50000000);
	MaterialFloat Local51 = (Local49 * 0.00000000);
	MaterialFloat Local52 = (View.GameTime * Material.PreshaderBuffer[2].x);
	MaterialFloat Local53 = (Local52 * 0.20000000);
	MaterialFloat Local54 = (Local52 * 0.00000000);
	MaterialFloat2 Local55 = (DERIV_BASE_VALUE(Local38) * ((MaterialFloat2)Material.PreshaderBuffer[2].y));
	MaterialFloat2 Local56 = (MaterialFloat2(Local53,Local54) + DERIV_BASE_VALUE(Local55));
	MaterialFloat Local57 = MaterialStoreTexCoordScale(Parameters, DERIV_BASE_VALUE(Local56), 1);
	MaterialFloat4 Local58 = ProcessMaterialColorTextureLookup(Texture2DSampleBias(Material_Texture2D_2,Material_Texture2D_2Sampler,DERIV_BASE_VALUE(Local56),View.MaterialTextureMipBias));
	MaterialFloat Local59 = MaterialStoreTexSample(Parameters, Local58, 1);
	MaterialFloat2 Local60 = (((MaterialFloat2)1.00000000) + Local58.rgb.rg);
	MaterialFloat2 Local61 = (Local60 * ((MaterialFloat2)0.50000000));
	MaterialFloat2 Local62 = (((MaterialFloat2)Material.PreshaderBuffer[2].z) * Local61);
	MaterialFloat2 Local63 = (Local62 + DERIV_BASE_VALUE(Local38));
	MaterialFloat2 Local64 = (Local63 * ((MaterialFloat2)Material.PreshaderBuffer[2].w));
	MaterialFloat2 Local65 = (MaterialFloat2(Local50,Local51) + Local64);
	MaterialFloat Local66 = MaterialStoreTexCoordScale(Parameters, Local65, 2);
	MaterialFloat4 Local67 = ProcessMaterialColorTextureLookup(Texture2DSampleBias(Material_Texture2D_3,Material_Texture2D_3Sampler,Local65,View.MaterialTextureMipBias));
	MaterialFloat Local68 = MaterialStoreTexSample(Parameters, Local67, 2);
	MaterialFloat Local69 = (Local49 * -0.30000001);
	MaterialFloat Local70 = (Local49 * 0.20000000);
	MaterialFloat2 Local71 = (MaterialFloat2(Local69,Local70) + Local65);
	MaterialFloat Local72 = MaterialStoreTexCoordScale(Parameters, Local71, 2);
	MaterialFloat4 Local73 = ProcessMaterialColorTextureLookup(Texture2DSampleBias(Material_Texture2D_4,Material_Texture2D_4Sampler,Local71,View.MaterialTextureMipBias));
	MaterialFloat Local74 = MaterialStoreTexSample(Parameters, Local73, 2);
	MaterialFloat Local75 = (Local67.r + Local73.r);
	MaterialFloat Local76 = (Local49 * 0.30000001);
	MaterialFloat2 Local77 = (MaterialFloat2(Local51,Local76) + Local64);
	MaterialFloat Local78 = MaterialStoreTexCoordScale(Parameters, Local77, 2);
	MaterialFloat4 Local79 = ProcessMaterialColorTextureLookup(Texture2DSampleBias(Material_Texture2D_5,Material_Texture2D_5Sampler,Local77,View.MaterialTextureMipBias));
	MaterialFloat Local80 = MaterialStoreTexSample(Parameters, Local79, 2);
	MaterialFloat Local81 = (Local75 * Local79.r);
	MaterialFloat Local82 = PositiveClampedPow(Local81,Material.PreshaderBuffer[3].x);
	MaterialFloat Local83 = (Local82 * Material.PreshaderBuffer[3].y);
	MaterialFloat Local84 = ddx(Local83);
	MaterialFloat Local85 = (Local83 + Local84);
	MaterialFloat Local86 = (Local85 - Local83);
	MaterialFloat3 Local87 = (((MaterialFloat3)Local86) * Local44);
	MaterialFloat Local88 = ddy(Local83);
	MaterialFloat Local89 = (Local83 + Local88);
	MaterialFloat Local90 = (Local89 - Local83);
	MaterialFloat3 Local91 = cross(Parameters.TangentToWorld[2],DERIV_BASE_VALUE(Local45));
	MaterialFloat3 Local92 = (((MaterialFloat3)Local90) * Local91);
	MaterialFloat3 Local93 = (Local87 + Local92);
	MaterialFloat Local94 = ((abs(Local46 - 0.00000000) > 0.00000000) ? ((Local46 >= 0.00000000) ? 1.00000000 : -1.00000000) : 0.00000000);
	MaterialFloat3 Local95 = (Local93 * ((MaterialFloat3)Local94));
	MaterialFloat3 Local96 = (Local48 - Local95);
	MaterialFloat3 Local97 = normalize(Local96);
	MaterialFloat3 Local98 = lerp(Local42,Local97,Local83);

	// The Normal is a special case as it might have its own expressions and also be used to calculate other inputs, so perform the assignment here
	PixelMaterialInputs.Normal = Local98;


#if TEMPLATE_USES_STRATA
	Parameters.SharedLocalBases = StrataInitialiseSharedLocalBases();
#endif

	// Note that here MaterialNormal can be in world space or tangent space
	float3 MaterialNormal = GetMaterialNormal(Parameters, PixelMaterialInputs);

#if MATERIAL_TANGENTSPACENORMAL
#if SIMPLE_FORWARD_SHADING
	Parameters.WorldNormal = float3(0, 0, 1);
#endif

#if FEATURE_LEVEL >= FEATURE_LEVEL_SM4
	// Mobile will rely on only the final normalize for performance
	MaterialNormal = normalize(MaterialNormal);
#endif

	// normalizing after the tangent space to world space conversion improves quality with sheared bases (UV layout to WS causes shrearing)
	// use full precision normalize to avoid overflows
	Parameters.WorldNormal = TransformTangentNormalToWorld(Parameters.TangentToWorld, MaterialNormal);

#else //MATERIAL_TANGENTSPACENORMAL

	Parameters.WorldNormal = normalize(MaterialNormal);

#endif //MATERIAL_TANGENTSPACENORMAL

#if MATERIAL_TANGENTSPACENORMAL
	// flip the normal for backfaces being rendered with a two-sided material
	Parameters.WorldNormal *= Parameters.TwoSidedSign;
#endif

	Parameters.ReflectionVector = ReflectionAboutCustomWorldNormal(Parameters, Parameters.WorldNormal, false);

#if !PARTICLE_SPRITE_FACTORY
	Parameters.Particle.MotionBlurFade = 1.0f;
#endif // !PARTICLE_SPRITE_FACTORY

	// Now the rest of the inputs
	MaterialFloat3 Local99 = lerp(MaterialFloat3(0.00000000,0.00000000,0.00000000),Material.PreshaderBuffer[4].xyz,Material.PreshaderBuffer[3].z);
	MaterialFloat Local100 = GetPixelDepth(Parameters);
	MaterialFloat Local101 = CalcSceneDepth(ScreenAlignedPosition(GetScreenPosition(Parameters)));
	MaterialFloat Local102 = (Local101 - DERIV_BASE_VALUE(Local100));
	MaterialFloat Local103 = (Local102 * Material.PreshaderBuffer[7].z);
	MaterialFloat Local104 = saturate(Local103);
	MaterialFloat Local105 = (1.00000000 * Local104);
	MaterialFloat3 Local106 = lerp(Material.PreshaderBuffer[9].xyz,Material.PreshaderBuffer[8].xyz,Local105);
	MaterialFloat3 Local107 = (Material.PreshaderBuffer[11].xyz + ((MaterialFloat3)Local83));
	MaterialFloat Local108 = (View.GameTime * Material.PreshaderBuffer[11].w);
	MaterialFloat Local109 = (Local108 * 0.00000000);
	MaterialFloat Local110 = (Local108 * -0.50000000);
	MaterialFloat2 Local111 = (Local61 * ((MaterialFloat2)Material.PreshaderBuffer[12].x));
	MaterialFloat2 Local112 = (Local111 + DERIV_BASE_VALUE(Local38));
	MaterialFloat2 Local113 = (Local112 * ((MaterialFloat2)Material.PreshaderBuffer[12].y));
	MaterialFloat2 Local114 = (MaterialFloat2(Local109,Local110) + Local113);
	MaterialFloat2 Local115 = ((MaterialFloat2(0.50000000,-0.50000000) * -1.00000000) + Local114);
	MaterialFloat Local116 = dot(Local115,Material.PreshaderBuffer[14].xy);
	MaterialFloat Local117 = dot(Local115,Material.PreshaderBuffer[14].zw);
	MaterialFloat2 Local118 = (MaterialFloat2(0.50000000,-0.50000000) + MaterialFloat2(Local116,Local117));
	MaterialFloat Local119 = MaterialStoreTexCoordScale(Parameters, Local118, 0);
	MaterialFloat4 Local120 = ProcessMaterialColorTextureLookup(Texture2DSampleBias(Material_Texture2D_6,Material_Texture2D_6Sampler,Local118,View.MaterialTextureMipBias));
	MaterialFloat Local121 = MaterialStoreTexSample(Parameters, Local120, 0);
	MaterialFloat Local122 = PositiveClampedPow(Local120.r,Material.PreshaderBuffer[15].y);
	MaterialFloat Local123 = (Local122 * Material.PreshaderBuffer[15].z);
	MaterialFloat Local124 = (View.GameTime * Material.PreshaderBuffer[15].w);
	MaterialFloat Local125 = (Local124 * 0.00000000);
	MaterialFloat Local126 = (Local124 * -0.50000000);
	MaterialFloat2 Local127 = (Local112 * ((MaterialFloat2)Material.PreshaderBuffer[16].x));
	MaterialFloat2 Local128 = (MaterialFloat2(Local125,Local126) + Local127);
	MaterialFloat2 Local129 = ((MaterialFloat2(0.50000000,-0.50000000) * -1.00000000) + Local128);
	MaterialFloat Local130 = dot(Local129,Material.PreshaderBuffer[18].xy);
	MaterialFloat Local131 = dot(Local129,Material.PreshaderBuffer[18].zw);
	MaterialFloat2 Local132 = (MaterialFloat2(0.50000000,-0.50000000) + MaterialFloat2(Local130,Local131));
	MaterialFloat Local133 = MaterialStoreTexCoordScale(Parameters, Local132, 0);
	MaterialFloat4 Local134 = ProcessMaterialColorTextureLookup(Texture2DSampleBias(Material_Texture2D_6,Material_Texture2D_6Sampler,Local132,View.MaterialTextureMipBias));
	MaterialFloat Local135 = MaterialStoreTexSample(Parameters, Local134, 0);
	MaterialFloat Local136 = PositiveClampedPow(Local134.r,Material.PreshaderBuffer[19].y);
	MaterialFloat Local137 = (Local136 * Material.PreshaderBuffer[19].z);
	MaterialFloat Local138 = (Local123 + Local137);
	MaterialFloat Local139 = (View.GameTime * Material.PreshaderBuffer[19].w);
	MaterialFloat Local140 = (Local139 * 0.00000000);
	MaterialFloat Local141 = (Local139 * -0.50000000);
	MaterialFloat2 Local142 = (Local112 * ((MaterialFloat2)Material.PreshaderBuffer[20].x));
	MaterialFloat2 Local143 = (MaterialFloat2(Local140,Local141) + Local142);
	MaterialFloat2 Local144 = ((MaterialFloat2(0.50000000,-0.50000000) * -1.00000000) + Local143);
	MaterialFloat Local145 = dot(Local144,Material.PreshaderBuffer[22].xy);
	MaterialFloat Local146 = dot(Local144,Material.PreshaderBuffer[22].zw);
	MaterialFloat2 Local147 = (MaterialFloat2(0.50000000,-0.50000000) + MaterialFloat2(Local145,Local146));
	MaterialFloat Local148 = MaterialStoreTexCoordScale(Parameters, Local147, 0);
	MaterialFloat4 Local149 = ProcessMaterialColorTextureLookup(Texture2DSampleBias(Material_Texture2D_6,Material_Texture2D_6Sampler,Local147,View.MaterialTextureMipBias));
	MaterialFloat Local150 = MaterialStoreTexSample(Parameters, Local149, 0);
	MaterialFloat Local151 = PositiveClampedPow(Local149.r,Material.PreshaderBuffer[23].y);
	MaterialFloat Local152 = (Local151 * Material.PreshaderBuffer[23].z);
	MaterialFloat Local153 = (Local138 + Local152);
	MaterialFloat4 Local154 = Parameters.VertexColor;
	MaterialFloat Local155 = DERIV_BASE_VALUE(Local154).r;
	MaterialFloat Local156 = lerp(Local153,0.00000000,DERIV_BASE_VALUE(Local155));
	MaterialFloat Local157 = PositiveClampedPow(Local156,Material.PreshaderBuffer[23].w);
	MaterialFloat Local158 = (Local157 * Material.PreshaderBuffer[3].y);
	MaterialFloat3 Local159 = lerp(Local106,Local107,Local158);
	MaterialFloat Local160 = (Local102 * Material.PreshaderBuffer[25].x);
	MaterialFloat Local161 = saturate(Local160);
	MaterialFloat Local162 = (Material.PreshaderBuffer[25].y * Local161);
	MaterialFloat Local213 = dot(WorldNormalCopy,Parameters.CameraVector);
	MaterialFloat Local214 = max(0.00000000,Local213);
	MaterialFloat Local215 = (1.00000000 - Local214);
	MaterialFloat Local216 = abs(Local215);
	MaterialFloat Local217 = max(Local216,0.00010000);
	MaterialFloat Local218 = PositiveClampedPow(Local217,Material.PreshaderBuffer[25].z);
	MaterialFloat Local219 = (Local218 * (1.00000000 - 0.04000000));
	MaterialFloat Local220 = (Local219 + 0.04000000);
	MaterialFloat Local221 = lerp(Material.PreshaderBuffer[25].w,0.00000000,Local220);

	PixelMaterialInputs.EmissiveColor = Local99;
	PixelMaterialInputs.Opacity = Local162;
	PixelMaterialInputs.OpacityMask = 1.00000000;
	PixelMaterialInputs.BaseColor = Local159;
	PixelMaterialInputs.Metallic = 0.00000000;
	PixelMaterialInputs.Specular = Material.PreshaderBuffer[24].x;
	PixelMaterialInputs.Roughness = Material.PreshaderBuffer[24].y;
	PixelMaterialInputs.Anisotropy = 0.00000000;
	PixelMaterialInputs.Normal = Local98;
	PixelMaterialInputs.Tangent = MaterialFloat3(1.00000000,0.00000000,0.00000000);
	PixelMaterialInputs.Subsurface = 0;
	PixelMaterialInputs.AmbientOcclusion = 1.00000000;
	PixelMaterialInputs.Refraction = MaterialFloat2(Local221,Material.PreshaderBuffer[26].x);
	PixelMaterialInputs.PixelDepthOffset = 0.00000000;
	PixelMaterialInputs.ShadingModel = 1;
	PixelMaterialInputs.FrontMaterial = GetInitialisedStrataData();


#if MATERIAL_USES_ANISOTROPY
	Parameters.WorldTangent = CalculateAnisotropyTangent(Parameters, PixelMaterialInputs);
#else
	Parameters.WorldTangent = 0;
#endif
}
		void surf( Input In, inout SurfaceOutputStandard o )
		{
			InitializeExpressions();

			float3 Z3 = float3( 0, 0, 0 );
			float4 Z4 = float4( 0, 0, 0, 0 );

			float3 UnrealWorldPos = float3( In.worldPos.x, In.worldPos.y, In.worldPos.z );
			
			float3 UnrealNormal = In.normal2;

			FMaterialPixelParameters Parameters = (FMaterialPixelParameters)0;
			#if NUM_TEX_COORD_INTERPOLATORS > 0			
				Parameters.TexCoords[ 0 ] = float2( In.uv_MainTex.x, 1.0 - In.uv_MainTex.y );
			#endif
			#if NUM_TEX_COORD_INTERPOLATORS > 1
				Parameters.TexCoords[ 1 ] = float2( In.uv2_Material_Texture2D_0.x, 1.0 - In.uv2_Material_Texture2D_0.y );
			#endif
			#if NUM_TEX_COORD_INTERPOLATORS > 2
			for( int i = 2; i < NUM_TEX_COORD_INTERPOLATORS; i++ )
			{
				Parameters.TexCoords[ i ] = float2( In.uv_MainTex.x, 1.0 - In.uv_MainTex.y );
			}
			#endif
			Parameters.PostProcessUV = In.uv_MainTex;
			Parameters.VertexColor = In.color;
			Parameters.WorldNormal = UnrealNormal;
			Parameters.ReflectionVector = half3( 0, 0, 1 );
			Parameters.CameraVector = normalize( _WorldSpaceCameraPos.xyz - UnrealWorldPos.xyz );
			//Parameters.CameraVector = mul( ( float3x3 )unity_CameraToWorld, float3( 0, 0, 1 ) ) * -1;
			Parameters.LightVector = half3( 0, 0, 0 );
			float4 screenpos = In.screenPos;
			screenpos /= screenpos.w;
			//screenpos.y = 1 - screenpos.y;
			Parameters.SvPosition = float4( screenpos.x, screenpos.y, 0, 0 );
			Parameters.ScreenPosition = Parameters.SvPosition;

			Parameters.UnMirrored = 1;

			Parameters.TwoSidedSign = 1;
			

			float3 InWorldNormal = UnrealNormal;
			float4 InTangent = In.tangent;
			float4 tangentWorld = float4( UnityObjectToWorldDir( InTangent.xyz ), InTangent.w );
			tangentWorld.xyz = normalize( tangentWorld.xyz );
			float3x3 OriginalTangentToWorld = CreateTangentToWorldPerVertex( InWorldNormal, tangentWorld.xyz, tangentWorld.w );
			Parameters.TangentToWorld = OriginalTangentToWorld;

			//WorldAlignedTexturing in UE relies on the fact that coords there are 100x larger, prepare values for that
			//but watch out for any computation that might get skewed as a side effect
			UnrealWorldPos = ToUnrealPos( UnrealWorldPos );
			
			Parameters.AbsoluteWorldPosition = UnrealWorldPos;
			Parameters.WorldPosition_CamRelative = UnrealWorldPos;
			Parameters.WorldPosition_NoOffsets = UnrealWorldPos;

			Parameters.WorldPosition_NoOffsets_CamRelative = Parameters.WorldPosition_CamRelative;
			Parameters.LightingPositionOffset = float3( 0, 0, 0 );

			Parameters.AOMaterialMask = 0;

			Parameters.Particle.RelativeTime = 0;
			Parameters.Particle.MotionBlurFade;
			Parameters.Particle.Random = 0;
			Parameters.Particle.Velocity = half4( 1, 1, 1, 1 );
			Parameters.Particle.Color = half4( 1, 1, 1, 1 );
			Parameters.Particle.TranslatedWorldPositionAndSize = float4( UnrealWorldPos, 0 );
			Parameters.Particle.MacroUV = half4(0,0,1,1);
			Parameters.Particle.DynamicParameter = half4(0,0,0,0);
			Parameters.Particle.LocalToWorld = float4x4( Z4, Z4, Z4, Z4 );
			Parameters.Particle.Size = float2(1,1);
			Parameters.Particle.SubUVCoords[ 0 ] = Parameters.Particle.SubUVCoords[ 1 ] = float2( 0, 0 );
			Parameters.Particle.SubUVLerp = 0.0;
			Parameters.TexCoordScalesParams = float2( 0, 0 );
			Parameters.PrimitiveId = 0;
			Parameters.VirtualTextureFeedback = 0;

			FPixelMaterialInputs PixelMaterialInputs = ( FPixelMaterialInputs)0;
			PixelMaterialInputs.Normal = float3( 0, 0, 1 );
			PixelMaterialInputs.ShadingModel = 0;

			View.GameTime = View.RealTime = _Time.y;// _Time is (t/20, t, t*2, t*3)
			View.PrevFrameGameTime = View.GameTime - unity_DeltaTime.x;//(dt, 1/dt, smoothDt, 1/smoothDt)
			View.PrevFrameRealTime = View.RealTime;
			View.DeltaTime = unity_DeltaTime.x;
			View.MaterialTextureMipBias = 0.0;
			View.TemporalAAParams = float4( 0, 0, 0, 0 );
			View.ViewRectMin = float2( 0, 0 );
			View.ViewSizeAndInvSize = View_BufferSizeAndInvSize;
			View.MaterialTextureDerivativeMultiply = 1.0f;
			View.StateFrameIndexMod8 = 0;
			View.FrameNumber = (int)_Time.y;
			View.FieldOfViewWideAngles = float2( PI * 0.42f, PI * 0.42f );//75degrees, default unity
			View.RuntimeVirtualTextureMipLevel = float4( 0, 0, 0, 0 );
			View.PreExposure = 0;
			View.BufferBilinearUVMinMax = float4(
				View_BufferSizeAndInvSize.z * ( 0 + 0.5 ),//EffectiveViewRect.Min.X
				View_BufferSizeAndInvSize.w * ( 0 + 0.5 ),//EffectiveViewRect.Min.Y
				View_BufferSizeAndInvSize.z * ( View_BufferSizeAndInvSize.x - 0.5 ),//EffectiveViewRect.Max.X
				View_BufferSizeAndInvSize.w * ( View_BufferSizeAndInvSize.y - 0.5 ) );//EffectiveViewRect.Max.Y

			for( int i2 = 0; i2 < 40; i2++ )
				View.PrimitiveSceneData[ i2 ] = float4( 0, 0, 0, 0 );

			float4x4 ViewMatrix = transpose( unity_MatrixV );
			float4x4 InverseViewMatrix = transpose( unity_MatrixInvV );
			float4x4 ViewProjectionMatrix = transpose( unity_MatrixVP );

			uint PrimitiveBaseOffset = Parameters.PrimitiveId * PRIMITIVE_SCENE_DATA_STRIDE;
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 0 ] = unity_ObjectToWorld[ 0 ];//LocalToWorld
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 1 ] = unity_ObjectToWorld[ 1 ];//LocalToWorld
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 2 ] = unity_ObjectToWorld[ 2 ];//LocalToWorld
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 3 ] = unity_ObjectToWorld[ 3 ];//LocalToWorld
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 6 ] = unity_WorldToObject[ 0 ];//WorldToLocal
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 7 ] = unity_WorldToObject[ 1 ];//WorldToLocal
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 8 ] = unity_WorldToObject[ 2 ];//WorldToLocal
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 9 ] = unity_WorldToObject[ 3 ];//WorldToLocal
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 10 ] = unity_WorldToObject[ 0 ];//PreviousLocalToWorld
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 11 ] = unity_WorldToObject[ 1 ];//PreviousLocalToWorld
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 12 ] = unity_WorldToObject[ 2 ];//PreviousLocalToWorld
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 13 ] = unity_WorldToObject[ 3 ];//PreviousLocalToWorld
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 18 ] = float4( ToUnrealPos( UNITY_MATRIX_M[ 3 ] ), 0 );//ActorWorldPosition
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 19 ] = LocalObjectBoundsMax - LocalObjectBoundsMin;//ObjectBounds
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 21 ] = mul( unity_ObjectToWorld, float3( 1, 0, 0 ) );//ObjectOrientation
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 23 ] = LocalObjectBoundsMin;//LocalObjectBoundsMin
			View.PrimitiveSceneData[ PrimitiveBaseOffset + 24 ] = LocalObjectBoundsMax;//LocalObjectBoundsMax

			#ifdef UE5
				ResolvedView.WorldCameraOrigin = LWCPromote( ToUnrealPos( _WorldSpaceCameraPos.xyz ) );
				ResolvedView.PreViewTranslation = LWCPromote( float3( 0, 0, 0 ) );
				ResolvedView.WorldViewOrigin = LWCPromote( float3( 0, 0, 0 ) );
			#else
				ResolvedView.WorldCameraOrigin = ToUnrealPos( _WorldSpaceCameraPos.xyz );
				ResolvedView.PreViewTranslation = float3( 0, 0, 0 );
				ResolvedView.WorldViewOrigin = float3( 0, 0, 0 );
			#endif
			ResolvedView.PrevWorldCameraOrigin = ResolvedView.WorldCameraOrigin;
			ResolvedView.ScreenPositionScaleBias = float4( 1, 1, 0, 0 );
			ResolvedView.TranslatedWorldToView = ViewMatrix;
			ResolvedView.TranslatedWorldToCameraView = ViewMatrix;
			ResolvedView.TranslatedWorldToClip = ViewProjectionMatrix;
			ResolvedView.ViewToTranslatedWorld = InverseViewMatrix;
			ResolvedView.PrevViewToTranslatedWorld = ResolvedView.ViewToTranslatedWorld;
			ResolvedView.CameraViewToTranslatedWorld = InverseViewMatrix;
			ResolvedView.BufferBilinearUVMinMax = View.BufferBilinearUVMinMax;
			ResolvedView.XRPassthroughCameraUVs[ 0 ] = ResolvedView.XRPassthroughCameraUVs[ 1 ] = float4( 0, 0, 1, 1 );
			Primitive.WorldToLocal = unity_WorldToObject;
			Primitive.LocalToWorld = unity_ObjectToWorld;
			CalcPixelMaterialInputs( Parameters, PixelMaterialInputs );

			#define HAS_WORLDSPACE_NORMAL 0
			#if HAS_WORLDSPACE_NORMAL
				PixelMaterialInputs.Normal = mul( PixelMaterialInputs.Normal, (MaterialFloat3x3)( transpose( OriginalTangentToWorld ) ) );
			#endif

			o.Albedo = PixelMaterialInputs.BaseColor.rgb;
			o.Alpha = PixelMaterialInputs.Opacity;
			//if( PixelMaterialInputs.OpacityMask < 0.333 ) discard;

			o.Metallic = PixelMaterialInputs.Metallic;
			o.Smoothness = 1.0 - PixelMaterialInputs.Roughness;
			o.Normal = normalize( PixelMaterialInputs.Normal );
			o.Emission = PixelMaterialInputs.EmissiveColor.rgb;
			o.Occlusion = PixelMaterialInputs.AmbientOcclusion;

			//BLEND_ADDITIVE o.Alpha = ( o.Emission.r + o.Emission.g + o.Emission.b ) / 3;
		}
		ENDCG
	}
	Fallback "Diffuse"
}