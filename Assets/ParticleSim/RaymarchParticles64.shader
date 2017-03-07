Shader "Custom/RaymarchParticles64"
{
	Properties {
		_ParticleTex ("Particle Texture", 2D) = "white" {}
		_HitLinkTex ("Hit Link Texture", 2D) = "white" {}
		_MissLinkTex ("Miss Link Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent" }
		//Blend One One
		//Blend SrcAlpha One
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
// Upgrade NOTE: excluded shader from DX11, OpenGL ES 2.0 because it uses unsized arrays
#pragma exclude_renderers d3d11 gles
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float3 objPos : TEXCOORD0;
				float3 objViewDir : TEXCOORD1;
			};
			
			v2f vert(appdata_base v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.objPos = v.vertex;
				o.objViewDir = -ObjSpaceViewDir(v.vertex);
				return o;
			}

			sampler2D _ParticleTex;
			sampler2D _HitLinkTex;
			sampler2D _MissLinkTex;

			float dist(float3 a, float3 b) {
				float3 d = a - b;
				return sqrt(d.x * d.x + d.y * d.y + d.z * d.z);
			}

			bool collides(float4 s1, float4 s2) {
				float3 relPos = float3(s1.x - s2.x, s1.y - s2.y, s1.z - s2.z);
				float  sqDist = relPos.x * relPos.x + relPos.y * relPos.y + relPos.z * relPos.z;
				float minDist = s1.w + s2.w;
				return sqDist <= minDist * minDist;
			}

			struct Node {
				float2 uv;
				int    mip;
				float4 sphere;
			};

			float4 getSphere(float2 uv, int mip) {
				if (mip < 0) return float4(0, 0, 0, 0);
				float4 sphere = tex2Dlod(_ParticleTex, float4(uv.x, uv.y, 0, mip));
				return float4(sphere.x - 0.5, sphere.y - 0.5, sphere.z - 0.5, sphere.w);
			}

			Node getNextNodeFrom(Node node, sampler2D linkTex) {
				float4 uvCoordLink = tex2Dlod(linkTex, float4(node.uv.x, node.uv.y, 0, node.mip));
				Node nextNode;
				nextNode.uv = float2(uvCoordLink.x, uvCoordLink.y);
				if (uvCoordLink.w /* null bit */ > 0.5) {
					nextNode.mip = -1;
				}
				else {
					nextNode.mip = (int)(uvCoordLink.z * 10.001);
				}
				nextNode.sphere = getSphere(nextNode.uv, nextNode.mip);
				return nextNode;
			}

			Node getHitNode(Node node) {
				return getNextNodeFrom(node, _HitLinkTex);
			}

			Node getMissNode(Node node) {
				return getNextNodeFrom(node, _MissLinkTex);
			}

			bool isNull(Node node) {
				return node.mip == -1;
			}

			bool isLeaf(Node node) {
				return isNull(getHitNode(node));
			}

			bool isLeafParent(Node node) {
				return isLeaf(getHitNode(node));
			}

			float dist(Node n, float3 pos) {
				return dist(n.sphere.xyz, pos) - n.sphere.w;
			}

			Node GetRootNode() {
				Node node;
				node.mip = 6;
				node.uv = float2(0.5, 0.5);
				return node;
			}

			struct Node4 {
				Node n0; Node n1; Node n2; Node n3;
			};

			Node4 Pop(Node n) {
				Node4 N;
				N.n0 = getHitNode(n);
				N.n1 = getMissNode(N.n0);
				N.n2 = getMissNode(N.n1);
				N.n3 = getMissNode(N.n2);
				return N;
			}

			Node getClosestNaively(Node4 nodes, float3 pos) {
				Node closestNode = nodes.n0;
				float smallestDist = dist(nodes.n0, pos), testDist;
				testDist = dist(nodes.n1, pos);
				if (testDist < smallestDist) { smallestDist = testDist; closestNode = nodes.n1; }
				testDist = dist(nodes.n2, pos);
				if (testDist < smallestDist) { smallestDist = testDist; closestNode = nodes.n2; }
				testDist = dist(nodes.n3, pos);
				if (testDist < smallestDist) { smallestDist = testDist; closestNode = nodes.n3; }
				return closestNode;
			}

			Node nthLargest(int n, float d0, Node n0, float d1, Node n1, float d2, Node n2, float d3, Node n3) {
				return n0;
			}

			Node4 SortNodesByDist(Node4 nodes, float3 pos) {
				Node4 sortedNodes;
				float d0 = dist(nodes.n0, pos);
				float d1 = dist(nodes.n1, pos);
				float d2 = dist(nodes.n2, pos);
				float d3 = dist(nodes.n3, pos);
				sortedNodes.n0 = nthLargest(0, d0, nodes.n0, d1, nodes.n1, d2, nodes.n2, d3, nodes.n3);
				sortedNodes.n1 = nthLargest(1, d0, nodes.n0, d1, nodes.n1, d2, nodes.n2, d3, nodes.n3);
				sortedNodes.n2 = nthLargest(2, d0, nodes.n0, d1, nodes.n1, d2, nodes.n2, d3, nodes.n3);
				sortedNodes.n3 = nthLargest(3, d0, nodes.n0, d1, nodes.n1, d2, nodes.n2, d3, nodes.n3);
				return sortedNodes;
			}

			Node getClosest_0(Node node, float3 pos) {
				return node;
			}

			Node getClosest(Node node, float3 pos) {
				if (isLeafParent(node)) {
					return getClosestNaively(Pop(node), pos); // OK because the nodes are particles (leaves)
				}
				else {
					Node4 children = Pop(node);
					Node4 sortedChildren = SortNodesByDist(children, pos);

					Node firstChild  = children.n0;
					Node closestNode = getClosest_0(firstChild, pos);
					float closestNodeDist = dist(closestNode, pos); // prune distance

					Node secondChild = children.n1;
					Node thirdChild  = children.n2;
					Node fourthChild = children.n3;

					float secondChildDist = dist(secondChild, pos);
					if (secondChildDist < closestNodeDist) {
						Node secondChildClosestNode = getClosest_0(secondChild, pos);
						float secondChildClosestNodeDist = dist(secondChildClosestNode, pos);
						if (secondChildClosestNodeDist < closestNodeDist) {
							closestNode = secondChildClosestNode;
							closestNodeDist = secondChildClosestNodeDist;
						}
					}

					float thirdChildDist = dist(thirdChild, pos);
					if (thirdChildDist < closestNodeDist) {
						Node thirdChildClosestNode = getClosest_0(thirdChild, pos);
						float thirdChildClosestNodeDist = dist(thirdChildClosestNode, pos);
						if (thirdChildClosestNodeDist < closestNodeDist) {
							closestNode = thirdChildClosestNode;
							closestNodeDist = thirdChildClosestNodeDist;
						}
					}

					float fourthChildDist = dist(fourthChild, pos);
					if (fourthChildDist < closestNodeDist) {
						Node fourthChildClosestNode = getClosest_0(fourthChild, pos);
						float fourthChildClosestNodeDist = dist(fourthChildClosestNode, pos);
						if (fourthChildClosestNodeDist < closestNodeDist) {
							closestNode = fourthChildClosestNode;
							closestNodeDist = fourthChildClosestNodeDist;
						}
					}

					return closestNode;
				}
			}

			float getParticleDistance(float3 pos) {
				return dist(getClosest(GetRootNode(), pos), pos);
			}

			float4 raymarchWithHeuristic(float3 rayOrigin, float3 rayDir) {
				int maxIterations = 10;
				int iterations = 0;
				float4 colorPerUnitDensity = float4(1, 1, 1, 1);
				float4 density = float4(0, 0, 0, 0);
				float3 pos = rayOrigin;
				float particleDistance;
				for (int i = 0; i < maxIterations; i++) {
					particleDistance = getParticleDistance(pos);
					if (particleDistance < 0.01) {
						//return colorPerUnitDensity * 1;
						break;
					}
					else {
						pos += rayDir * particleDistance;
					}
					iterations++;
				}
				float iterFrac = iterations / (float)maxIterations;
				if (iterFrac < 0.5) {
					return lerp(float4(1, 1, 1, 1), float4(0.2, 0.5, 1, 1), iterFrac * 2);
				}
				else {
					return lerp(float4(0.2, 0.5, 1, 1), float4(1, 0, 0, 0.3), (iterFrac - 0.5) * 2);
				}
				// return colorPerUnitDensity * density;
			}

			float queryObjSpaceParticleDensity(float3 pos) {
				return 0;
			}

			float4 raymarchParticles(float3 rayOrigin, float3 rayDir) {
				float stepSize = 0.01;
				float4 colorPerUnitDensity = float4(1, 1, 1, 1);
				float4 color = float4(0, 0, 0, 0);
				for (int i = 0; i < 16; i++) {
					float outputDensity = queryObjSpaceParticleDensity(rayDir * stepSize * i + rayOrigin);
					color += outputDensity * colorPerUnitDensity;
					if (outputDensity >= 1) break;
				}
				return color;
			}

			// for now just returns the distance of the ray to the largest sphere
			// float unsignedClosestDistToRay(Node node, float3 pos, float3 dir) {
			// 	float3 sphereFromRayPos = node.sphere.xyz - pos;
			// 	float sphereFromRayPosMag = sqrt(dot(sphereFromRayPos, sphereFromRayPos));
			// 	float ABcosTheta_closestDistAlongRayToSphere = saturate(dot(sphereFromRayPos, dir));
			// 	float ABsinTheta_closestDistFromSphere = sin(acos(ABcosTheta_closestDistAlongRayToSphere / sphereFromRayPosMag)) * sphereFromRayPosMag;


			// 	//float distToSphere = dot(sphereFromRayPos)
			// 	return saturate(ABsinTheta_closestDistFromSphere - node.sphere.w);
			// }

			#define PI 3.14159
			float RaycastSphere(float4 sphere, float3 rayPos, float3 rayDir, out bool hit) {
				float3 spherePos = sphere.xyz;
				float3 sphereFromRay = spherePos - rayPos;
				float radiusSqrd = sphere.w * sphere.w;
				if (dot(spherePos, spherePos) < radiusSqrd) {
					hit = true;
					return 0;
				}

				float3 normSphereFromRay = normalize(sphereFromRay);
				float raySphereAngle = acos(dot(normSphereFromRay, rayDir));
				if (raySphereAngle >= PI) {
					hit = false;
					return 10000;
				}

				float closestDistToSphere = sphereFromRay / sin(raySphereAngle);
				if (closestDistToSphere * closestDistToSphere > radiusSqrd) {
					hit = false;
					return 10000;
				}

				float halfPenetrationDist = dot(sphereFromRay, rayDir);
				
			}

			float4 raycastSphericQuadtree(float3 pos, float3 dir) {
				float4 color = 0;

				Node root = GetRootNode();
				bool hit;
				float distanceToSphere = RaycastSphere(root.sphere, pos, dir, out hit);

				return color;
			}
			
			fixed4 frag(v2f i) : SV_Target
			{
				float4 col = raycastSphericQuadtree(i.objPos, normalize(i.objViewDir));
				return col;
			}
			ENDCG
		}
	}
}
