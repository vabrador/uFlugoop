using System.Collections.Generic;
using UnityEngine;

namespace uFlugoop {
  
  public class Particle64Sim : MonoBehaviour {

    public Material _particle64SimMaterial;

    [SerializeField]
    private Texture2D _particleTexture;
    [SerializeField]
    private Texture2D _hitLinkTexture;
    [SerializeField]
    private Texture2D _missLinkTexture;

    public GameObject toAssignTexture;
    public GameObject particleVisualizer;
    private Color[] particles;
    private Color[] hitLinks;
    private Color[] missLinks;
    public GameObject[] spawnedVisualizers;

    void Start() {
      Texture2D particleTex = GenerateParticles(64);
      InitializeParticleSystem(particleTex);

      MeshRenderer renderer = GetComponent<MeshRenderer>();
      if (renderer == null) {
        Debug.LogError("No renderer found. Try adding the script to Unity Cube primitive.");
      }
      else {
        _particle64SimMaterial.SetTexture("_ParticleTex", _particleTexture);
        _particle64SimMaterial.SetTexture("_HitLinkTex", _hitLinkTexture);
        _particle64SimMaterial.SetTexture("_MissLinkTex", _missLinkTexture);
        renderer.sharedMaterial = _particle64SimMaterial;

        toAssignTexture.GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_MainTex", _particleTexture);
      }
    }

    //float angleVel = 1F;
    //void Update() {
    //  this.transform.Rotate(Vector3.up * angleVel);
    //}

    private Texture2D GenerateParticles(int sqRootWidth) {
      TexturePixelArray particles = new TexturePixelArray(new Color[sqRootWidth * sqRootWidth], sqRootWidth);
      for (int i = 0; i < particles.Width; i++) {
        for (int j = 0; j < particles.Height; j++) {
          Vector3 rV = Random.insideUnitSphere;
          particles[i, j] = new Sphere(Vector3.one * 0.5F + (rV * 0.8F), 0.005F);
        }
      }

      Texture2D tex = new Texture2D(sqRootWidth, sqRootWidth, TextureFormat.RGBAFloat, true);
      tex.filterMode = FilterMode.Point;
      tex.SetPixels(particles.Pixels);
      tex.Apply(false, false);
      return tex;
    }

    private void InitializeParticleSystem(Texture2D particleTex) {
      _particleTexture = particleTex;
      SphericQuadtree.SortTexture(_particleTexture);
      ConstructSphereMipMaps(_particleTexture);
      
      if (_missLinkTexture == null || _hitLinkTexture == null) {
        int numMipMaps = (int)Mathf.Log(_particleTexture.width, 2) + 1;
        Debug.Log("Constructing link textures with " + numMipMaps + " mipmap levels");

        Util.CreateLinkTextures(_particleTexture.width, out _hitLinkTexture, out _missLinkTexture);
      }
    }
    
    /// <summary>Constructs spherical mipmaps for input texture,
    /// interpreting RGBA as XYZR and calculating the smallest
    /// containing sphere for a given 4-texel square.</summary>
    private static void ConstructSphereMipMaps(Texture2D tex) {
      if (tex.width != tex.height) {
        Debug.LogError("Texture must be power-of-2 and square.");
      }

      int numMipMaps = (int)Mathf.Log(tex.width, 2) + 1;
      int curMipWidth = tex.width;
      for (int sourceMipLevel = 0; sourceMipLevel < numMipMaps - 1; sourceMipLevel++) {
        Color[] sourcePixels = tex.GetPixels(sourceMipLevel);
        Color[] mippedPixels = tex.GetPixels(sourceMipLevel + 1);
        for (int i = 0; i < curMipWidth; i += 2) {
          for (int j = 0; j < curMipWidth; j += 2) {
            // s0 - s3
            // |    |
            // s1 - s2
            Sphere s0 = sourcePixels[(i)   + curMipWidth * (j)  ];
            Sphere s1 = sourcePixels[(i)   + curMipWidth * (j+1)];
            Sphere s2 = sourcePixels[(i+1) + curMipWidth * (j+1)];
            Sphere s3 = sourcePixels[(i+1) + curMipWidth * (j)  ];
            
            mippedPixels[(i / 2) + (curMipWidth / 2) * (j / 2)] = CalculateSmallestContainingSphere(s0, s1, s2, s3);
          }
        }

        tex.SetPixels(mippedPixels, sourceMipLevel + 1);
        curMipWidth = curMipWidth / 2;
      }

      tex.Apply(false, false);
    }

    private static Sphere CalculateSmallestContainingSphere(Sphere s0, Sphere s1, Sphere s2, Sphere s3) {
      Vector3 s0Pos = s0.Position;
      Vector3 s1Pos = s1.Position;
      Vector3 s2Pos = s2.Position;
      Vector3 s3Pos = s3.Position;

      Vector3 avgPosition = AveragePosition(s0Pos, s1Pos, s2Pos, s3Pos);

      Sphere farthestSphere = s0;
      Vector3 farthestSpherePos = s0Pos;
      float farthestSquareDist = Util.SquareDistance(s0Pos, avgPosition);
      float testSquareDist = Util.SquareDistance(s1Pos, avgPosition);
      if (testSquareDist > farthestSquareDist) {
        farthestSphere = s1;
        farthestSpherePos = s1Pos;
        farthestSquareDist = testSquareDist;
      }
      testSquareDist = Util.SquareDistance(s2Pos, avgPosition);
      if (testSquareDist > farthestSquareDist) {
        farthestSphere = s2;
        farthestSpherePos = s2Pos;
        farthestSquareDist = testSquareDist;
      }
      testSquareDist = Util.SquareDistance(s3Pos, avgPosition);
      if (testSquareDist > farthestSquareDist) {
        farthestSphere = s3;
        farthestSpherePos = s3Pos;
        farthestSquareDist = testSquareDist;
      }

      float smallestContainingSphereRadius = Vector3.Distance(farthestSpherePos, avgPosition) + (farthestSphere.r);

      return new Sphere(avgPosition, smallestContainingSphereRadius);
    }

    private static Vector3 AveragePosition(Vector3 a, Vector3 b, Vector3 c, Vector3 d) {
      return new Vector3((a.x + b.x + c.x + d.x),
                         (a.y + b.y + c.y + d.y),
                         (a.z + b.z + c.z + d.z)) / 4F;
    }

  }
}