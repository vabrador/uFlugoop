using System.Collections.Generic;
using UnityEngine;

namespace uFlugoop {

	public static class Util {

		/// <summary> Creates hit and miss link textures with correct mipmaps,
		/// using the given texWidth as the base texture width. (Must be power-of-two.)
		/// </summary>
		public static void CreateLinkTextures(int texWidth, out Texture2D hitLinkTexture, out Texture2D missLinkTexture) {
			hitLinkTexture = new Texture2D(texWidth, texWidth, TextureFormat.RGBAFloat, true);
			hitLinkTexture.filterMode = FilterMode.Point;
      hitLinkTexture.alphaIsTransparency = false;
      missLinkTexture = new Texture2D(texWidth, texWidth, TextureFormat.RGBAFloat, true);
			missLinkTexture.filterMode = FilterMode.Point;
      missLinkTexture.alphaIsTransparency = false;

      QuadMipLinkTurtle linkWalker = new QuadMipLinkTurtle(texWidth);
			bool stillWalking = true;
			do {
				stillWalking = linkWalker.Step();
			} while (stillWalking);

			linkWalker.FillLinkTextures(hitLinkTexture, missLinkTexture);
			hitLinkTexture.Apply(false, false);
			missLinkTexture.Apply(false, false);
			return;
		}

		public static Vector3 RGBtoXYZ(Color pixel) {
			return ((Sphere)pixel).Position;
		}

		public static float SquareDistance(Vector3 a, Vector3 b) {
			return Mathf.Abs(a.sqrMagnitude - b.sqrMagnitude);
		}

	}

	/// <summary> Helper class for traversing every pixel coordinate in a
	/// texture and its mipmaps, in spheric-quadtree order.
	/// First Step() initializes, each Step() returning true while steps remain,
	/// along with output variables describing the hit and miss links for each
	/// stepped node. (The operation automatically jumps along one of these nodes
	/// to walk the whole texture tree.) </summary>
	internal class QuadMipLinkTurtle {
		private int x;
		private int y;
		private int mipLevel;
		private int[] mipWidths;
		private int MipCount { get { return mipWidths.Length; } }
		private int TexWidth { get { return mipWidths[0]; } }
		private List<Color[]> hitLinkPixels  = new List<Color[]>();
		private List<Color[]> missLinkPixels = new List<Color[]>();
		private bool finishing = false;
		private bool finished = false;
		private UVCoordLink curHitLink;
		private UVCoordLink curMissLink;
		
		public QuadMipLinkTurtle(int texWidth) {
			int mipCount = (int)Mathf.Log(texWidth, 2) + 1;
			mipWidths = new int[mipCount];
			Debug.Log("Initialized turtle; MipCount is " + MipCount);
			int curMipWidth = texWidth;
			for (int i = 0; i < mipCount; i++) {
				mipWidths[i] = curMipWidth;
				hitLinkPixels.Add(new Color[curMipWidth * curMipWidth]);
			  missLinkPixels.Add(new Color[curMipWidth * curMipWidth]);
				curMipWidth /= 2;
			}

			this.x = -1;
			this.y = -1;
			this.mipLevel = -1;
		}

		public void FillLinkTextures(Texture2D hitLinkTex, Texture2D missLinkTex) {
			if (!finished) {
				Debug.LogError("Step to completion first.");
				return;
			}
			else {
				for (int m = 0; m < MipCount; m++) {
					hitLinkTex .SetPixels( hitLinkPixels[m], m);
					missLinkTex.SetPixels(missLinkPixels[m], m);
				}
			}
		}

		// Hit Links: Down one mip level if possible (x * 2, y * 2), else null
		// Miss Links:
		// s0    s3 --> (up-and-next until a non-visited node is found,)
		// |     ^       or null if a miss ends traversal)
		// v     |
		// s1 -> s2
		/// <summary> Sets the output hitLink and missLink coordinate links for the node 
		/// just arrived at by the Step(). Returns true if there are more steps to take,
		/// false otherwise. </summary>
		public bool Step() {
			//Debug.Log("Stepping.");
			if (finished) { Debug.LogError("Already finished walking."); return true; }
			// Initialization case
			if (mipLevel == -1 && x == -1 && y == -1) {
				this.x = 0;
				this.y = 0;
				this.mipLevel = mipWidths.Length - 1;
				
				UVCoordLink hitLink, missLink;
				GetRootNodeLinks(out hitLink, out missLink);

				this.curHitLink = hitLink;
				this.hitLinkPixels [mipLevel][x + mipWidths[mipLevel] * y] = hitLink;
				this.curMissLink = missLink;
				this.missLinkPixels[mipLevel][x + mipWidths[mipLevel] * y] = missLink;
				if (hitLink.isNull) {
					Debug.LogWarning("Special case detected: Base texture is one pixel.");
					finished = true;
				}
				return !finished;
			}
			else {
				if (!finishing) {
					if (curHitLink.isNull) {
						// Follow miss link.
						this.x 				= curMissLink.X(TexWidth);
						// Debug.Log("TexWidth is: " + TexWidth);
						// Debug.Log("MipLevelInteger is: " + curMissLink.MipLevelInteger + " and MipWidth is " + curMissLink.MipWidth(curMissLink.MipLevelInteger, TexWidth));
						// Debug.Log("Miss link X: " + curMissLink.X(TexWidth) + "; Miss link Y: " + curHitLink.Y(TexWidth));
						this.y 				= curMissLink.Y(TexWidth);
						this.mipLevel = curMissLink.MipLevelInteger;
						//Debug.Log("Followed miss link: " + curMissLink + ". Current position is: " + this);
					}
					else {
						// Follow hit link.
						this.x        = curHitLink.X(TexWidth);
						this.y        = curHitLink.Y(TexWidth);
						this.mipLevel = curHitLink.MipLevelInteger;
						//Debug.Log("Followed hit link: " + curHitLink  + ". Current position is: " + this);
					}
					
					UVCoordLink hitLink, missLink;
					GetLinksForNode(this.x, this.y, this.mipLevel, out hitLink, out missLink);

					this.curHitLink = hitLink;
					this.hitLinkPixels [mipLevel][x + mipWidths[mipLevel] * y] = hitLink;
					this.curMissLink = missLink;
					this.missLinkPixels[mipLevel][x + mipWidths[mipLevel] * y] = missLink;

					if (this.x == mipWidths[mipLevel] - 1 && this.y == 0 && mipLevel == 0) {
						// Hit upper-right corner of the mip-0 texture, start finishing.
						//Debug.Log("Finishing began.");
						finishing = true;
					}
					else {
						return true;
					}
				}
				if (finishing) {
					// To finish, travel up one mip level to successive upper-right corners.
					return ClimbUpperRightCorner();
				}

				//Debug.LogError("Should never reach here.");
				return true;
			}
		}

		public override string ToString() {
			return "[QuadMipLinkTurtle] x: " + x + ", y: " + y + ", mip: " + mipLevel
					 + ", curHitLink: " + curHitLink + ", curMissLink: " + curMissLink;
		}

		private bool ClimbUpperRightCorner() {
			x /= 2;
			y /= 2;
			mipLevel += 1;

			if (mipLevel == MipCount - 1) finished = true;
			else {

				UVCoordLink hitLink = Link(x * 2, y * 2, mipLevel - 1);
				UVCoordLink missLink = NullLink();

				try {
					this.curHitLink = hitLink;
					this.hitLinkPixels [mipLevel][x + mipWidths[mipLevel] * y] = hitLink;
					this.curMissLink = missLink;
					this.missLinkPixels[mipLevel][x + mipWidths[mipLevel] * y] = missLink;
				}
				catch (System.IndexOutOfRangeException) {
					Debug.LogError("tried to index for mipLevel: " + mipLevel + " and x " + x + " and y " + y);
				}
			}

			return !finished;
		}

		private void GetLinksForNode(int x, int y, int mipLevel, out UVCoordLink hitLink, out UVCoordLink missLink) {
			if (x % 2 == 0 && y % 2 == 0) {
				if (mipLevel == MipCount - 1) {
					GetRootNodeLinks(out hitLink, out missLink);
				}
				else {
					GetUpperLeftLinks(x, y, mipLevel, out hitLink, out missLink);
				}
			}
			else if (x % 2 == 0 && y % 2 == 1) {
				GetLowerLeftLinks(x, y, mipLevel, out hitLink, out missLink);
			}
			else if (x % 2 == 1 && y % 2 == 1) {
				GetLowerRightLinks(x, y, mipLevel, out hitLink, out missLink);
			}
			else /* (i % 2 == 1 && j % 2 == 0) */ {
				GetUpperRightLinks(x, y, mipLevel, out hitLink, out missLink);
			}
		}
		
		private void GetRootNodeLinks(out UVCoordLink hitLink, out UVCoordLink missLink) {
			if (MipCount == 1) {
				//Debug.LogWarning("Special case detected again: Single pixel texture.");
				hitLink = UVCoordLink.NullLink();
			}
			else {
				//Debug.Log("Root node link. MipCount is " + MipCount);
				hitLink = new UVCoordLink(0, 0, MipCount - 2, 2);
				//Debug.Log("GetRootNodeLink. Returning hit link: " + hitLink);
			}
			missLink = UVCoordLink.NullLink();
		}

		private UVCoordLink Link(int x, int y, int mipLevel) {
			if (mipLevel == -1) {
				return UVCoordLink.NullLink();
			}
			else {
				return new UVCoordLink(x, y, mipLevel, mipWidths[mipLevel]);
			}
		}

		private UVCoordLink NullLink() {
			return UVCoordLink.NullLink();
		}

		private void GetUpperLeftLinks(int x, int y, int mipLevel, out UVCoordLink hitLink, out UVCoordLink missLink) {
			// Hit link: Move down a mip level.
			hitLink = Link(x * 2, y * 2, mipLevel - 1);
			// Miss link: Move down.
			missLink = Link(x, y + 1, mipLevel);
		}

		private void GetLowerLeftLinks(int x, int y, int mipLevel, out UVCoordLink hitLink, out UVCoordLink missLink) {
			// Hit link: Move down a mip level.
			hitLink = Link(x * 2, y * 2, mipLevel - 1);
			// Miss link: Move right.
			missLink = Link(x + 1, y, mipLevel);
		}

		private void GetLowerRightLinks(int x, int y, int mipLevel, out UVCoordLink hitLink, out UVCoordLink missLink) {
			// Hit link: Move down a mip level.
			hitLink = Link(x * 2, y * 2, mipLevel - 1);
			// Miss link: Move up.
			missLink = Link(x, y - 1, mipLevel);
		}

		private void GetUpperRightLinks(int x, int y, int mipLevel, out UVCoordLink hitLink, out UVCoordLink missLink) {
			// Hit link: Move down a mip level.
			hitLink = Link(x * 2, y * 2, mipLevel - 1);
			// Miss link: Move up one mip level until a non-upper-right square is reached,
			// then move to the next square in that sequence.
			// Or stop if the mip limit is reached (null link).
			int newX = x, newY = y, newMipLevel = mipLevel;
			do {
				newMipLevel += 1;
				newX /= 2;
				newY /= 2;
			} while (newX % 2 == 1 && newY % 2 == 0 && mipLevel != MipCount - 1);
			if (newX % 2 == 0 && newY % 2 == 0) {
				if (newMipLevel == MipCount - 1) {
					missLink = NullLink(); return;
				}
				else {
					missLink = Link(newX, newY + 1, newMipLevel);
				}
			}
			else if (newX % 2 == 0 && newY % 2 == 1) {
				missLink = Link(newX + 1, newY, newMipLevel);
			}
			else /* (newX % 2 == 0 && newY % 2 == 1) */ {
				missLink = Link(newX, newY - 1, newMipLevel);
			}
		}
	}

	/// <summary> Link encoded for Color.
	/// RGBA -> UVMN "U, V, MipLevel, (is)Null". <summary>
	public struct UVCoordLink {
		public float u;
		public float v;
		public float mipLevel;
		public bool isNull;

		public UVCoordLink (int x, int y, int mipLevel, float mippedTexSqRootSize) {
			this.u = (x + 0.5F) / mippedTexSqRootSize;
			this.v = (y + 0.5F) / mippedTexSqRootSize;
			this.mipLevel = mipLevel / 10F;
			this.isNull = false;
		}

		public int X(int texWidth) { return (int)(MipWidth(MipLevelInteger, texWidth) * u); }
		public int Y(int texWidth) { return (int)(MipWidth(MipLevelInteger, texWidth) * v); }
		public int MipWidth(int mipLevel, int texWidth) { return (int)Mathf.Pow(2, (int)Mathf.Log(texWidth, 2) - mipLevel); }
		public int MipLevelInteger { get { return (int)(mipLevel * 10.0001F); } }

		public static UVCoordLink NullLink() {
			return new UVCoordLink(0);
		}
		private UVCoordLink(int zzUnused) {
			this.u = 0;
			this.v = 0;
			this.mipLevel = 0;
			this.isNull = true;
		}

		public override string ToString() {
			return "[UVCoordLink] u: " + u.ToString("R") + ", v: " + v.ToString("R") + ", mipLevel: " + mipLevel + ", isNull: " + isNull;
		}

		public static implicit operator Color(UVCoordLink uv) {
			return new Color (uv.u, uv.v, uv.mipLevel, uv.isNull ? 1F : 0F);
		}
	}

}
