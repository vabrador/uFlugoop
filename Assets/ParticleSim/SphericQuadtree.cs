using System.Collections.Generic;
using UnityEngine;

namespace uFlugoop {

	/// <summary> Provides static helper functions for spatially
	/// sorting texture pixels into an optimized arrangement for
	/// spherical quadtree traversal. In a nutshell, pixel RGB
	/// values are interpreted as XYZ point coordinates, and
	/// the points are recursively sorted and bisected using
	/// each recursive point-set's axis of greatest deviation. </summary>
	public static class SphericQuadtree {
		
		public static void SortTexture(Texture2D tex) {
			Color[] pixelsArr = tex.GetPixels();

			TexturePixelArray pixels = new TexturePixelArray(pixelsArr, tex.width);
			SortTexturePixelArray(pixels, Vector3.right);

			tex.SetPixels(pixelsArr);
			tex.Apply(false, false);
		}

		/// <summary> Recursively sorts the underlying TexturePixelArray array
		/// into an optimized arrangement for quadtrees by sorting and recursively
		/// re-sorting half partitions horizontally and vertically by that partition's
		/// axis of greatest difference from the partition centroid. </summary>
		private static void SortTexturePixelArray(TexturePixelArray pixels) {
			Vector3 sortingAxis = FindAxisOfGreatestDeviation(pixels);
			SortTexturePixelArray(pixels, sortingAxis);
		}

		private static void SortTexturePixelArray(TexturePixelArray pixels, Vector3 sortingAxis) {
			QuickSort(pixels, new AxisComparer(sortingAxis));

			// Bisection & recursion
			TexturePixelArray half0, half1;
			pixels.Bisect(out half0, out half1);
			if (half0.Count >= 2) {
				half0.FlipIndexingOrder();
				SortTexturePixelArray(half0, NextSortingAxis(sortingAxis));
			}
			if (half1.Count >= 2) {
				half1.FlipIndexingOrder();
				SortTexturePixelArray(half1, NextSortingAxis(sortingAxis));
			}
		}

		private static bool VerifySort(TexturePixelArray pixels, IComparer<Color> pixelComparer) {
			for (int i = 0; i < pixels.Count - 1; i++) {
				if (pixelComparer.Compare(pixels[i], pixels[i+1]) > 0) {
					return false;
				}
			}
			return true;
		}

		private static Vector3 NextSortingAxis(Vector3 origSortingAxis) {
			if (origSortingAxis.x > 0.5) {
				return Vector3.up;
			}
			else if (origSortingAxis.y > 0.5) {
				return Vector3.forward;
			}
			else {
				return Vector3.right;
			}
		}

		private static void QuickSort(TexturePixelArray pixels, IComparer<Color> pixelComparer) {
			QuickSort(pixels, 0, pixels.Count - 1, pixelComparer);
		}
		private static void QuickSort(TexturePixelArray pixels, int lowIdx, int highIdx, IComparer<Color> pixelComparer) {
			int i = lowIdx, j = highIdx;
			Color pivot = pixels[(lowIdx + highIdx) / 2];
			while (i <= j) {
				while (pixelComparer.Compare(pixels[i], pivot) < 0) { i++; }
				while (pixelComparer.Compare(pixels[j], pivot) > 0) { j--; }
				if (i <= j) {
					pixels.Swap(i, j);
					i++; j--;
				}
			}
			if (lowIdx < j) {
				QuickSort(pixels, lowIdx, j, pixelComparer);
			}
			if (i < highIdx) {
				QuickSort(pixels, i, highIdx, pixelComparer);
			}
		}

		private class AxisComparer : IComparer<Color> {
			private Vector3 axis;

			public AxisComparer(Vector3 axis) {
				this.axis = axis;
			}

			public int Compare(Color x, Color y) {
				return Vector3.Dot(Util.RGBtoXYZ(x), axis).CompareTo(Vector3.Dot(Util.RGBtoXYZ(y), axis));
			}
		}

		private static Vector3 FindAxisOfGreatestDeviation(TexturePixelArray pixels) {
			Vector3 centroid = AverageXYZ(pixels);
			Vector3 farthestPoint = FindFarthestPoint(pixels, centroid);
			return farthestPoint.normalized;
		}

		private static Vector3 AverageXYZ(TexturePixelArray pixels) {
			Vector3 sum = Vector3.zero;
			for (int k = 0; k < pixels.Count; k++) {
				sum += Util.RGBtoXYZ(pixels[k]);
			}
			return sum / (float)pixels.Count;
		}

		private static Vector3 FindFarthestPoint(TexturePixelArray pixels, Vector3 fromPoint) {
			Vector3 farthestPoint = Util.RGBtoXYZ(pixels[0]);
			float farthestSqDist = Util.SquareDistance(farthestPoint, fromPoint);
			for (int k = 1; k < pixels.Count; k++) {
				Vector3 testPoint = Util.RGBtoXYZ(pixels[k]);
				float testSqDist = Util.SquareDistance(farthestPoint, testPoint);
				if (testSqDist > farthestSqDist) {
					farthestSqDist = testSqDist;
					farthestPoint = testPoint;
				}
			}
			return farthestPoint;
		}

	}

	/// <summary> Small helper class for indexing into an unwrapped texture pixel
	/// array using x, y coordinates, i.e. pixels[x * texWidth + y].
	/// For allocation-free array recursive partitioning, also supports
	/// specifying subsets of the underlying array via a Rect. </summary>
	public class TexturePixelArray {

		private Color[] _pixels;
		private int _texWidth;
		private IntRect _subset;

		/// <summary> Read only. Returns the IntRect subset of this TexturePixelArray. </summary>
		public IntRect subset { get { return _subset; } }

		/// <summary> Read only. Returns the X position of the pixel array subset.
		/// (Zero if no subset specified). </summary>
		public int X { get { return _subset.x; } }

		/// <summary> Read only. Returns the Y position of the pixel array subset.
		/// (Zero if no subset specified). </summary>
		public int Y { get { return _subset.y; } }

		/// <summary> Read only. Returns the width of the pixel array subset.
		/// (Whole array if no subset specified). </summary>
		public int Width { get { return _subset.width; } }

		/// <summary> Read only. Returns the height of the pixel array subset.
		/// (Whole array if no subset specified). </summary>
		public int Height { get { return _subset.height; } }

		/// <summary> IList<T> Implementation. Read only. Returns the number
		/// of pixels in the pixel array subset. (Of the whole array if no
		/// subset specified). </summary>
		public int Count { get { return _subset.width * _subset.height; } }

		/// <summary> Whether one-dimensional indexing should walk row-by-row
		/// or column-by-column. Only affects one-dimensional indexing;
		/// this[x, y] will function the same regardless of this setting. </summary>
		public enum IndexingOrder {
			RowByRow,
			ColumnByColumn
		}
		private IndexingOrder _indexingOrder = IndexingOrder.RowByRow;
		public IndexingOrder indexingOrder {
			get { return _indexingOrder; }
		}

		public TexturePixelArray(Color[] pixels, int texWidth)
		: this(pixels, texWidth,
						new IntRect(0, 0, texWidth, texWidth),
						indexingOrder: IndexingOrder.RowByRow) { }

		public TexturePixelArray(Color[] pixels, int texWidth, IntRect subset, IndexingOrder indexingOrder = IndexingOrder.RowByRow) {
			this._pixels = pixels;
			this._texWidth = texWidth;
			this._subset = subset;
			this._indexingOrder = indexingOrder;
		}
		
		/// <summary> Splits the TexturePixelArray into two halves, horizontally
		/// if the indexingOrder is RowByRow, vertically otherwise. The two returned
		/// TexturePixelArray objects reference the same underlying pixel array.</summary>
		public void Bisect(out TexturePixelArray half0, out TexturePixelArray half1) {
			IntRect half0Rect, half1Rect;
			if (indexingOrder == IndexingOrder.RowByRow) { // two long rectangles
				half0Rect = new IntRect( 		        X,     				  Y, Width    , Height / 2);
				half1Rect = new IntRect(            X, Height / 2 + Y, Width    , Height / 2);
			}
			else { 												 // ColumnByColumn, two thin rectangles
				half0Rect = new IntRect( 			      X,              Y, Width / 2, Height    );
				half1Rect = new IntRect(Width / 2 + X, 							Y, Width / 2, Height    );
			}

			half0 = new TexturePixelArray(_pixels, _texWidth, half0Rect, indexingOrder: _indexingOrder);
			half1 = new TexturePixelArray(_pixels, _texWidth, half1Rect, indexingOrder: _indexingOrder);
		}
		
		// Old: quadrisection method used while debugging bisection and recursion algorithm.
		// /// <summary> Splits the TexturePixelArray into four quarters.</summary>
		// public void Quadrisect(out TexturePixelArray arr0, out TexturePixelArray arr1,
		// 											 out TexturePixelArray arr2, out TexturePixelArray arr3) {
		// 	IntRect arr0Rect = new IntRect( 		       X,     				 Y, Width / 2, Height / 2);
		// 	IntRect arr1Rect = new IntRect(Width / 2 + X, 					   Y, Width / 2, Height / 2);
		// 	IntRect arr2Rect = new IntRect( 		       X, Height / 2 + Y, Width / 2, Height / 2);
		// 	IntRect arr3Rect = new IntRect(Width / 2 + X, Height / 2 + Y, Width / 2, Height / 2);

		// 	arr0 = new TexturePixelArray(_pixels, _texWidth, arr0Rect, indexingOrder: _indexingOrder);
		// 	arr1 = new TexturePixelArray(_pixels, _texWidth, arr1Rect, indexingOrder: _indexingOrder);
		// 	arr2 = new TexturePixelArray(_pixels, _texWidth, arr2Rect, indexingOrder: _indexingOrder);
		// 	arr3 = new TexturePixelArray(_pixels, _texWidth, arr3Rect, indexingOrder: _indexingOrder);
		// }

		public void FlipIndexingOrder() {
			if (this._indexingOrder == IndexingOrder.ColumnByColumn) {
				this._indexingOrder = IndexingOrder.RowByRow;
			}
			else {
				this._indexingOrder = IndexingOrder.ColumnByColumn;
			}
		}

		/// <summary> Two-dimensional accessor that respects Rect subset specification.
		/// Careful: Does not raise extra out-of-bounds exceptions through the subset.
		/// </summary>
		public Color this[int x, int y] {
			get {
				return _pixels[(x + _subset.x) + (_texWidth) * (y + _subset.y)];
			}
			set {
				_pixels[(x + _subset.x) + _texWidth * (y + _subset.y)] = value;
			}
		}

		/// <summary> One-dimensional accessor that respects Rect subset specification.
		/// Flattened row-by-row. Careful: Does not raise extra out-of-bounds exceptions
		/// through the subsets. </summary>
		public Color this[int k] {
			get {
				int i, j;
				if (_indexingOrder == IndexingOrder.RowByRow) {
					i = k % _subset.width;
					j = k / _subset.width;
				}
				else { // ColumnByColumn
					i = k / _subset.height;
					j = k % _subset.height;
				}
				return this[i, j];
			}
			set {
				int i, j;
				if (_indexingOrder == IndexingOrder.RowByRow) {
					i = k % _subset.width;
					j = k / _subset.width;
				}
				else { // ColumnByColumn
					i = k / _subset.height;
					j = k % _subset.height;
				}
				this[i, j] = value;
			}
		}

		public void Swap(int k0, int k1) {
			Color temp = this[k0];
			this[k0] = this[k1];
			this[k1] = temp;
		}

		/// <summary> Read only. Returns the underlying pixel array,
		/// suitable for Texture2D.SetPixels(). </summary>
		public Color[] Pixels {
			get { return _pixels; }
		}

 }

 public struct IntRect {
		public int x;
		public int y;
		public int width;
		public int height;

		public IntRect(int x, int y, int width, int height) {
			this.x = x; this.y = y; this.width = width; this.height = height;
		}

		public override string ToString() {
			return "[IntRect] x: " + x + ", y: " + y + ", width: " + width + ", height: " + height;
		}
	}

	/// <summary> Sphere encoded for Color.
	/// RGBA -> XYZR "X, Y, Z, Radius". </summary>
	public struct Sphere {
		public float x;
		public float y;
		public float z;
		public float r;

		public Sphere(float x, float y, float z, float r) {
			this.x = x; this.y = y; this.z = z; this.r = r;
		}

		public Sphere(Vector3 pos, float r) {
			this.x = pos.x; this.y = pos.y; this.z = pos.z; this.r = r;
		}

		public static implicit operator Sphere(Color c) {
			return new Sphere(c.r, c.g, c.b, c.a);
		}
		public static implicit operator Color(Sphere s) {
			return new Color(s.x, s.y, s.z, s.r);
		}

		public Vector3 Position {
			get { return new Vector3(x, y, z); }
		}

		public override string ToString() {
			return "[Sphere] Position: " + new Vector3(x, y, z).ToString("G4") + "; Radius: " + r;
		}
	}
}