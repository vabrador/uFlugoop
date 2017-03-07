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

			struct Node {
				float2 uv;
				int    mip;
				float4 sphere;
			};

			float4 getSphere(float2 uv, int mip) {
				if (mip < 0) return float4(0, 0, 0, 0);
				float4 sphere = tex2Dlod(_ParticleTex, float4(uv.x, uv.y, 0, mip));
				return float4(sphere.x - 0.5, sphere.y - 0.5, sphere.z - 0.5, sphere.w / 2);
			}

			Node getNextNodeFrom(Node node, sampler2D linkTex) {
				float4 uvCoordLink = tex2Dlod(linkTex, float4(node.uv.x, node.uv.y, 0, node.mip));
				Node nextNode;
				nextNode.uv = float2(uvCoordLink.x, uvCoordLink.y);
				if (uvCoordLink.w /* null bit */ > 0.5) {
					nextNode.mip = -1;
				}
				else {
					nextNode.mip = (int)(uvCoordLink.z * 10.0001);
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

      Node getNextNode(Node node, bool wasHit) {
        if (wasHit) {
          return getHitNode(node);
        }
        else {
          return getMissNode(node);
        }
      }

			bool isNull(Node node) {
				return node.mip == -1;
			}

			bool isLeaf(Node node) {
				return isNull(getHitNode(node));
			}

			Node GetRootNode() {
				Node node;
				node.mip = 7;
				node.uv = float2(0.5, 0.5);
        node.sphere = getSphere(node.uv, node.mip);
				return node;
			}

      inline float3 onPlane(float3 v, float3 n) {
        return v - n * dot(v, n);
      }

      // Returns the vector from rayPos in rayDir that intersections with the surface of the sphere,
      // or zero and false hit if the ray does not intersect the sphere.
      float3 RaycastSphere(float4 sphere, float3 rayPos, float3 rayDir, out bool hit, out float3 hitNormal) {
        float3 sphereFromRay = sphere.xyz - rayPos;
        float3 b = onPlane(sphereFromRay, rayDir);
        float r = sphere.w;
        hit = dot(b, b) < r * r;
        float3 fromPosToHalfPenetration = sphereFromRay - b;
        float  halfPenetrationAmount = sqrt(r * r - b * b);
        float3 intersectionFromRay = fromPosToHalfPenetration - rayDir * halfPenetrationAmount;
        hitNormal = normalize(intersectionFromRay - sphereFromRay);
        return intersectionFromRay;
      }

      bool RaycheckSphere(float4 sphere, float3 rayPos, float3 rayDir) {
        float3 sphereFromRay = sphere.xyz - rayPos;
        float3 b = onPlane(sphereFromRay, rayDir);
        float r = sphere.w;
        return dot(b, b) < r * r;
      }

      #define NUM_ITERATIONS 64

			float4 raycastSphericQuadtree(float3 pos, float3 dir) {
				/*Node node = GetRootNode();
				bool hit;
				float3 hitFromRayPos = RaycastSphere(node.sphere, pos, dir, hit);
        pos += hitFromRayPos;*/

        Node node = GetRootNode();
        
        bool hit;
        bool hitLeaf = false;
        while (!isNull(node)) {
          hit = RaycheckSphere(node.sphere, pos, dir);
          //float3 intersectionFromRay = RaycastSphere(node.sphere, pos, dir, hit, tempNormal);
          hitLeaf = hitLeaf || (hit && isLeaf(node));
          node = getNextNode(node, hit);
        }

        float4 color = 0;
        if (hitLeaf) color = 1;
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

/*

// old node code

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

//float4 raymarchWithHeuristic(float3 rayOrigin, float3 rayDir) {
//	int maxIterations = 10;
//	int iterations = 0;
//	float4 colorPerUnitDensity = float4(1, 1, 1, 1);
//	float4 density = float4(0, 0, 0, 0);
//	float3 pos = rayOrigin;
//	float particleDistance;
//	for (int i = 0; i < maxIterations; i++) {
//		particleDistance = getParticleDistance(pos);
//		if (particleDistance < 0.01) {
//			//return colorPerUnitDensity * 1;
//			break;
//		}
//		else {
//			pos += rayDir * particleDistance;
//		}
//		iterations++;
//	}
//	float iterFrac = iterations / (float)maxIterations;
//	if (iterFrac < 0.5) {
//		return lerp(float4(1, 1, 1, 1), float4(0.2, 0.5, 1, 1), iterFrac * 2);
//	}
//	else {
//		return lerp(float4(0.2, 0.5, 1, 1), float4(1, 0, 0, 0.3), (iterFrac - 0.5) * 2);
//	}
// return colorPerUnitDensity * density;
}

*/
