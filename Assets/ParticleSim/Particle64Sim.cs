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
    private int visualizeIdx;
    private float particleVizTimer = 0F;
    private float particleVizTime = 0.02F;
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
        renderer.material = _particle64SimMaterial;

        toAssignTexture.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", _particleTexture);
      }
    }

    float angleVel = 1F;

    void Update() {
      particleVizTimer += Time.deltaTime;
      if (particleVizTimer > particleVizTime) {
        //MoveParticleViz();
        particleVizTimer = 0;
      }

      this.transform.Rotate(Vector3.up * angleVel);
    }

    private void MoveParticleViz() {
      if (particles == null) {
        particles = _particleTexture.GetPixels();
        missLinks = _missLinkTexture.GetPixels();
        hitLinks = _hitLinkTexture.GetPixels();
        spawnedVisualizers = new GameObject[particles.Length];
      }
      int stepCount = 4;
      for (int i = 0; i < stepCount; i++) {
        visualizeIdx += 2;
        if (visualizeIdx >= particles.Length) {
          visualizeIdx %= particles.Length;
          visualizeIdx += 1;
        }
        particleVisualizer.transform.position = this.transform.TransformPoint(Util.RGBtoXYZ(particles[visualizeIdx]) - Vector3.one * 0.5F);
        //Debug.Log("Miss link at idx " + visualizeIdx + ": " + missLinks[visualizeIdx]);
        //Debug.Log("Hit link at idx " + visualizeIdx + ": " + hitLinks[visualizeIdx]);
        //spawnedVisualizers[visualizeIdx] = Instantiate<GameObject>(particleVisualizer);
        //spawnedVisualizers[visualizeIdx].transform.parent = this.transform;
      }
    }

    private Texture2D GenerateParticles(int sqRootWidth) {
      TexturePixelArray particles = new TexturePixelArray(new Color[sqRootWidth * sqRootWidth], sqRootWidth);
      for (int i = 0; i < particles.Width; i++) {
        for (int j = 0; j < particles.Height; j++) {
          Vector3 rV = Random.insideUnitSphere;
          particles[i, j] = new Sphere(Vector3.one * 0.5F + (rV * 0.4F), 0.03F);
        }
      }

      Texture2D tex = new Texture2D(sqRootWidth, sqRootWidth);
      tex.filterMode = FilterMode.Point;
      tex.SetPixels(particles.Pixels);
      tex.Apply(false, false);
      return tex;
    }

    private void InitializeParticleSystem(Texture2D particleTex) {
      _particleTexture = particleTex;
      SphericQuadtree.SortTexture(_particleTexture);
      ConstructSphereMipMaps(_particleTexture);

      int numMipMaps = (int)Mathf.Log(_particleTexture.width, 2) + 1;
      Debug.Log("Constructing link textures with " + numMipMaps + " mipmap levels");
      Util.CreateLinkTextures(_particleTexture.width, out _hitLinkTexture, out _missLinkTexture);
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

      float smallestContainingSphereRadius = Vector3.Distance(farthestSpherePos, avgPosition) + farthestSphere.r;

      return new Sphere(avgPosition, smallestContainingSphereRadius);
    }

    private static Vector3 AveragePosition(Vector3 a, Vector3 b, Vector3 c, Vector3 d) {
      return new Vector3((a.x + b.x + c.x + d.x),
                         (a.y + b.y + c.y + d.y),
                         (a.z + b.z + c.z + d.z)) / 4F;
    }

    // private static Texture2D ConstructMissLinkTexture2(int texWidth) {
    //   int numMips = (int)Mathf.Log(texWidth, 2) + 1;  
    //   Texture2D missLinkTex = new Texture2D(texWidth, texWidth);
    //   missLinkTex.filterMode = FilterMode.Point;

    //   int curMipWidth = texWidth;
    //   for (int curMipLevel = 0; curMipLevel < numMips; curMipLevel++) {
    //     TexturePixelArray pixels = new TexturePixelArray(new Color[curMipWidth * curMipWidth], curMipWidth);

    //     if (pixels.Count == 1) {
    //       Debug.Log("MissLink: Yes, reached one pixel.");
    //       pixels[0] = UVCoordLink.NullLink();
    //     }
    //     else {
    //       Debug.Log("Calculating miss link mip level: " + curMipLevel + "; pixel count is " + pixels.Count);
    //       for (int i = 0; i < pixels.Width; i += 1) {
    //         for (int j = 0; j < pixels.Height; j += 1) {
    //           Color pixelColor;
    //           if (i % 2 == 0 && j % 2 == 0) {
    //             // upper-left corner texel: connect down by one.
    //             pixelColor = new UVCoordLink(i, j+1, curMipLevel, curMipWidth);
    //           }
    //           else if (i % 2 == 0 && j % 2 == 1) {
    //             // lower-left corner texel: connect right by one.
    //             pixelColor = new UVCoordLink(i+1, j, curMipLevel, curMipWidth);
    //           }
    //           else if (i % 2 == 1 && j % 2 == 1) {
    //             // lower-right corner texel: connect up by one.
    //             pixelColor = new UVCoordLink(i, j-1, curMipLevel, curMipWidth);
    //           }
    //           else /* (i % 2 == 1 && j % 2 == 0) */ {
    //             // upper-right corner texel: connect to next-in-sequence texel one mip up, or NULL if none.
    //             int i_mipUp = i / 2, j_mipUp = j / 2;

    //             if (i_mipUp % 2 == 0 && j_mipUp % 2 == 0) {
    //               // corresponds to upper-left corner, connect down.
    //               pixelColor = new UVCoordLink(i_mipUp, j_mipUp + 1, curMipLevel + 1, curMipWidth / 2);
    //             }
    //             else if (i_mipUp % 2 == 0 && j % 2 == 1) {
    //               // corresponds to lower-left corner, connect right.
    //               pixelColor = new UVCoordLink(i_mipUp + 1, j_mipUp, curMipLevel + 1, curMipWidth / 2);
    //             }
    //             else if (i_mipUp % 2 == 1 && j_mipUp % 2 == 1) {
    //               // corresponds to lower-right corner, connect up.
    //               pixelColor = new UVCoordLink(i_mipUp, j_mipUp - 1, curMipLevel + 1, curMipWidth / 2);
    //             }
    //             else {
    //               // corresponds to upper-right corner, connect NULL.
    //               pixelColor = UVCoordLink.NullLink();
    //             }
    //           }

    //           pixels[i, j] = pixelColor;
    //         }
    //       }
    //     }

    //     missLinkTex.SetPixels(pixels.Pixels, curMipLevel);
    //     curMipWidth /= 2;
    //   }

    //   missLinkTex.Apply(false, false);
    //   return missLinkTex;
    // }

    // private static Texture2D ConstructMissLinkTexture(int texWidth, int numMips) {
    //   Texture2D tex = new Texture2D(texWidth, texWidth);
    //   tex.filterMode = FilterMode.Point;
    //   int curMipWidth = texWidth;

    //   for (int curMipLevel = 0; curMipLevel < numMips; curMipLevel++) {

    //     Color[] texColors = tex.GetPixels(curMipLevel);
    //     for (int k = 0; k < texColors.Length; k++) {
    //       int i = k % curMipWidth;
    //       int j = k / curMipWidth;
          
    //       // Texel sequence:
    //       // s0    s3 --> (next-in-sequence one-mip-up texel, or NULL if none)
    //       // |     ^
    //       // v     |
    //       // s1 -> s2
    //       Color pixelColor;
    //       if (i % 2 == 0 && j % 2 == 0) {
    //         // upper-left corner texel: connect down by one.
    //         pixelColor = new UVCoordLink(i, j+1, curMipLevel, curMipWidth);
    //       }
    //       else if (i % 2 == 0 && j % 2 == 1) {
    //         // lower-left corner texel: connect right by one.
    //         pixelColor = new UVCoordLink(i+1, j, curMipLevel, curMipWidth);
    //       }
    //       else if (i % 2 == 1 && j % 2 == 1) {
    //         // lower-right corner texel: connect up by one.
    //         pixelColor = new UVCoordLink(i, j-1, curMipLevel, curMipWidth);
    //       }
    //       else if (i % 2 == 1 && j % 2 == 0) {
    //         // upper-right corner texel: connect to next-in-sequence texel one mip up, or NULL if none.
    //         int i_mipUp = i / 2, j_mipUp = j / 2;
    //         if (i_mipUp % 2 == 0 && j_mipUp % 2 == 0) {
    //           // corresponds to upper-left corner, connect down if possible.
    //           if (curMipLevel == numMips - 1) {
    //             // highest mipLevel reached, null link
    //             pixelColor = UVCoordLink.NullLink();
    //           }
    //           // otherwise, OK to link to next texel
    //           pixelColor = new UVCoordLink(i_mipUp, j_mipUp + 1, curMipLevel + 1, curMipWidth / 2);
    //         }
    //         else if (i_mipUp % 2 == 0 && j % 2 == 1) {
    //           // corresponds to lower-left corner, connect right.
    //           pixelColor = new UVCoordLink(i_mipUp + 1, j_mipUp, curMipLevel + 1, curMipWidth / 2);
    //         }
    //         else if (i_mipUp % 2 == 1 && j_mipUp % 2 == 1) {
    //           // corresponds to lower-right corner, connect up.
    //           pixelColor = new UVCoordLink(i_mipUp, j_mipUp - 1, curMipLevel + 1, curMipWidth / 2);
    //         }
    //         else {
    //           // corresponds to upper-right corner, connect NULL.
    //           pixelColor = UVCoordLink.NullLink();
    //         }
    //       }
    //       else {
    //         Debug.LogError("Hole in square-location logic; this should never be called.");
    //         pixelColor = UVCoordLink.NullLink();
    //       }

    //       texColors[k] = pixelColor;
    //     }

    //     tex.SetPixels(texColors, curMipLevel);
    //     curMipWidth /= 2;
    //   }

    //   tex.Apply(false, false);

    //   return tex;
    // }

    // private static Texture2D ConstructHitLinkTexture(int texWidth, int numMips) {
    //   Texture2D tex = new Texture2D(texWidth, texWidth);
    //   tex.filterMode = FilterMode.Point;

    //   // (Mip Level Zero) Hit Link Leaves
    //   Color[] nullLinks = new Color[texWidth * texWidth];
    //   for (int i = 0; i < nullLinks.Length; i++) {
    //     nullLinks[i] = UVCoordLink.NullLink();
    //   }
    //   tex.SetPixels(nullLinks);

    //   // (Mip Level Nonzero) Hit Link Branches
    //   for (int curMipLevel = 1; curMipLevel < numMips; curMipLevel++) {
    //     Color[] sourcePixels = tex.GetPixels(curMipLevel);
    //     for (int k = 0; k < sourcePixels.Length; k++) {
    //       // each i, j pair connects one mip-level down, upper-left corner
    //       int i = k % tex.width;
    //       int j = k / tex.width;
    //       int hitXCoord = i * 2, hitYCoord = j * 2;
    //       sourcePixels[k] = new UVCoordLink(hitXCoord, hitYCoord, curMipLevel-1, tex.width);
    //     }
    //     tex.SetPixels(sourcePixels, curMipLevel);
    //   }

    //   tex.Apply(false, false);

    //   return tex;
    // }

  }
}