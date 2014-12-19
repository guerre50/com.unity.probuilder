using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ProBuilder2.Common;
using ProBuilder2.Math;

namespace ProBuilder2.MeshOperations
{
	public static class pbTriangleOps
	{
		/**
		 *	\brief Reverse the winding order for each passed #pb_Face.
		 *	@param faces The faces to apply normal flippin' to.
		 *	\returns Nothing.  No soup for you.
		 *	\sa SelectedFaces pb_Face
		 */
		public static void ReverseWindingOrder(this pb_Object pb, pb_Face[] faces)
		{
			for(int i = 0; i < faces.Length; i++)
				faces[i].ReverseIndices();
		}	

		/**
		 * Attempt to figure out the winding order the passed face.  Note that 
		 * this may return WindingOrder.Unknown.
		 */
		public static WindingOrder GetWindingOrder(this pb_Object pb, pb_Face face)
		{
			Vector2[] p = pb_Math.PlanarProject(pb.GetVertices( face.edges.AllTriangles() ), pb_Math.Normal(pb, face));

			float sum = 0f;

			// http://stackoverflow.com/questions/1165647/how-to-determine-if-a-list-of-polygon-points-are-in-clockwise-order
			for(int i = 0; i < p.Length; i++)
			{
				Vector2 a = p[i];
				Vector2 b = i < p.Length - 1 ? p[i+1] : p[0];

				sum += ( (b.x-a.x) * (b.y+a.y) );
			}

			return sum == 0f ? WindingOrder.Unknown : (sum >= 0f ? WindingOrder.Clockwise : WindingOrder.CounterClockwise);
		}

		/**
		 *	Iterates through all triangles in a pb_Object and removes triangles with area <= 0 and 
		 *	tris with indices that point to the same vertex.
		 * \returns True if Degenerate tris were found, false if no changes.
		 */
		public static bool RemoveDegenerateTriangles(this pb_Object pb, out int[] removed)
		{
			pb_IntArray[] sharedIndices = pb.sharedIndices;
			Vector3[] v = pb.vertices;
			List<pb_Face> del = new List<pb_Face>();

			List<pb_Face> f = new List<pb_Face>();

			foreach(pb_Face face in pb.faces)
			{
				List<int> tris = new List<int>();
		
				int[] ind = face.indices;
				for(int i = 0; i < ind.Length; i+=3)
				{
					int[] s = new int[3]
					{
						sharedIndices.IndexOf(ind[i+0]),
						sharedIndices.IndexOf(ind[i+1]),
						sharedIndices.IndexOf(ind[i+2])
					};

					float area = pb_Math.TriangleArea(v[ind[i+0]], v[ind[i+1]], v[ind[i+2]]);

					if( (s[0] == s[1] || s[0] == s[2] || s[1] == s[2]) || area <= 0 )
					{
						// don't include this face in the reconstruct
						;
					}
					else
					{
						tris.Add(ind[i+0]);
						tris.Add(ind[i+1]);
						tris.Add(ind[i+2]);
					}
				}

				if(tris.Count > 0)
				{
					face.SetIndices(tris.ToArray());
					face.RebuildCaches();

					f.Add(face);
				}
				else
				{
					del.Add(face);
				}
			}

			pb.SetFaces(f.ToArray());

			removed = pb.RemoveUnusedVertices();
			return removed.Length > 0;
		}
			
		/**
		 *	Removes triangles that occupy the same space and point to the same vertices.
		 */
		public static int[] RemoveDuplicateTriangles(this pb_Object pb)
		{
			pb_IntArray[] sharedIndices = pb.sharedIndices;
			Vector3[] v = pb.vertices;
			List<pb_Face> del = new List<pb_Face>();

			int[] removedIndices;

			List<pb_Face> f = new List<pb_Face>();

			foreach(pb_Face face in pb.faces)
			{
				List<int> tris = new List<int>();
		
				int[] ind = face.indices;
				for(int i = 0; i < ind.Length; i+=3)
				{
					int[] s = new int[3]
					{
						sharedIndices.IndexOf(ind[i+0]),
						sharedIndices.IndexOf(ind[i+1]),
						sharedIndices.IndexOf(ind[i+2])
					};

					float area = pb_Math.TriangleArea(v[ind[i+0]], v[ind[i+1]], v[ind[i+2]]);

					if( (s[0] == s[1] || s[0] == s[2] || s[1] == s[2]) || area <= 0 )
					{
						// don't include this face in the reconstruct
						;
					}
					else
					{
						tris.Add(ind[i+0]);
						tris.Add(ind[i+1]);
						tris.Add(ind[i+2]);
					}
				}

				if(tris.Count > 0)
				{
					face.SetIndices(tris.ToArray());
					face.RebuildCaches();

					f.Add(face);
				}
				else
				{
					del.Add(face);
				}
			}

			pb.SetFaces(f.ToArray());

			removedIndices = pb.RemoveUnusedVertices();

			return removedIndices;
		}

		/**
		 * Merge all faces into a sigle face.
		 */
		public static pb_Face MergeFaces(this pb_Object pb, pb_Face[] faces)
		{
			List<int> collectedIndices = new List<int>(faces[0].indices);
			
			for(int i = 1; i < faces.Length; i++)
			{
				collectedIndices.AddRange(faces[i].indices);
			}

			pb_Face mergedFace = new pb_Face(collectedIndices.ToArray(),
			                                 faces[0].material,
			                                 faces[0].uv,
			                                 faces[0].smoothingGroup,
			                                 faces[0].textureGroup,
			                                 faces[0].elementGroup,
			                                 faces[0].manualUV);

			pb_Face[] rebuiltFaces = new pb_Face[pb.faces.Length - faces.Length + 1];

			int n = 0;
			foreach(pb_Face f in pb.faces)
			{
				if(System.Array.IndexOf(faces, f) < 0)
				{
					rebuiltFaces[n++] = f;
				}
			}
			
			rebuiltFaces[n] = mergedFace;

			pb.SetFaces(rebuiltFaces);

			// merge vertices that are on top of one another now that they share a face
			Dictionary<int, int> shared = new Dictionary<int, int>();

			for(int i = 0; i < mergedFace.indices.Length; i++)
			{
				int sharedIndex = pb.sharedIndices.IndexOf(mergedFace.indices[i]);

				if(shared.ContainsKey(sharedIndex))
				{
					mergedFace.indices[i] = shared[sharedIndex];
				}
				else
				{
					shared.Add(sharedIndex, mergedFace.indices[i]);
				}
			}

			pb.RemoveUnusedVertices();

			return mergedFace;
		}
	}
}