Shader "Custom/RaymarchSphere"
{
	Properties { }
	SubShader
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent" }
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float3 objPos : TEXCOORD0;
				float3 objViewDir : TEXCOORD1;
			};
			
			v2f vert(appdata_full v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.objPos = v.vertex;
				o.objViewDir = -ObjSpaceViewDir(v.vertex);
				return o;
			}

			float4 queryObjSpaceParticleDensity(float3 objSpacePos) {
				float distFromOriginSqr = objSpacePos.x * objSpacePos.x
										+ objSpacePos.y * objSpacePos.y
										+ objSpacePos.z * objSpacePos.z;
				float4 invis = float4(1, 1, 1, 0);
				float4 white = float4(1, 1, 1, 1);
				return lerp(white, invis, saturate(sqrt(distFromOriginSqr) * 2));
			}

			float4 raymarchParticles(float3 rayOrigin, float3 rayDir) {
				float stepSize = 0.01;
				float4 color = float4(0, 0, 0, 0);
				for (int i = 0; i < 50; i++) {
					color += queryObjSpaceParticleDensity(rayDir * stepSize * i + rayOrigin);
				}
				return color;
			}
			
			fixed4 frag(v2f i) : SV_Target
			{
				float4 col = raymarchParticles(i.objPos, i.objViewDir);
				return col;
			}
			ENDCG
		}
	}
}
