using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.ProBuilder;
using KdTree;
using KdTree.Math;

namespace UnityEngine.ProBuilder.MeshOperations
{
	/// <summary>
	/// Methods for merging and splitting common (or shared) vertexes.
	/// </summary>
	public static class VertexEditing
	{
        /// <summary>
        /// Collapses all passed indexes to a single shared index.
        /// </summary>
        /// <remarks>
        /// Retains vertex normals.
        /// </remarks>
        /// <param name="mesh">Target mesh.</param>
        /// <param name="indexes">The indexes to merge to a single shared vertex.</param>
        /// <param name="collapseToFirst">If true, instead of merging all vertexes to the average position, the vertexes will be collapsed onto the first vertex position.</param>
        /// <returns>The first available local index created as a result of the merge. -1 if action is unsuccessfull.</returns>
        public static int MergeVertexes(this ProBuilderMesh mesh, int[] indexes, bool collapseToFirst = false)
		{
            if (mesh == null)
                throw new ArgumentNullException("mesh");

            if (indexes == null)
                throw new ArgumentNullException("indexes");

            Vertex[] vertexes = Vertex.GetVertexes(mesh);

			Vertex cen = collapseToFirst ? vertexes[indexes[0]] : Vertex.Average(vertexes, indexes);

			IntArray[] sharedIndexes = mesh.sharedIndexesInternal;
			IntArray[] sharedIndexesUV = mesh.sharedIndexesUVInternal;

			int newIndex = IntArrayUtility.MergeSharedIndexes(ref sharedIndexes, indexes);
			IntArrayUtility.MergeSharedIndexes(ref sharedIndexesUV, indexes);

			mesh.sharedIndexesInternal = sharedIndexes;
			mesh.sharedIndexesUVInternal = sharedIndexesUV;

			mesh.SetSharedVertexValues(newIndex, cen);

			int[] mergedSharedIndex = mesh.GetSharedIndexes()[newIndex].array;

			int[] removedIndexes = mesh.RemoveDegenerateTriangles();

			// get a non-deleted index to work with
			int ind = -1;
			for(int i = 0; i < mergedSharedIndex.Length; i++)
				if(!removedIndexes.Contains(mergedSharedIndex[i]))
					ind = mergedSharedIndex[i];

			int res = ind;

			for(int i = 0; i < removedIndexes.Length; i++)
				if(ind > removedIndexes[i])
					res--;

            return res;
		}

		/// <summary>
		/// Split the vertexes referenced by edge from their shared indexes so that each vertex moves independently.
		/// </summary>
		/// <remarks>
		/// This is equivalent to calling `SplitVertexes(mesh, new int[] { edge.x, edge.y });`.
		/// </remarks>
		/// <param name="mesh">The source mesh.</param>
		/// <param name="edge">The edge to query for vertex indexes.</param>
		/// <seealso cref="SplitVertexes(UnityEngine.ProBuilder.ProBuilderMesh,System.Collections.Generic.IEnumerable{int})"/>
		public static void SplitVertexes(this ProBuilderMesh mesh, Edge edge)
		{
			SplitVertexes(mesh, new int[] { edge.a, edge.b });
		}

		/// <summary>
		/// Split vertexes from their shared indexes so that each vertex moves independently.
		/// </summary>
		/// <param name="mesh">The source mesh.</param>
		/// <param name="vertexes">A list of vertex indexes to split.</param>
		/// <seealso cref="UnityEngine.ProBuilder.ProBuilderMesh.sharedIndexes"/>
		public static void SplitVertexes(this ProBuilderMesh mesh, IEnumerable<int> vertexes)
		{
            if (mesh == null)
                throw new ArgumentNullException("mesh");

            if (vertexes == null)
                throw new ArgumentNullException("vertexes");

            // ToDictionary always sets the universal indexes in ascending order from 0+.
            Dictionary<int, int> lookup = mesh.sharedIndexesInternal.ToDictionary();
			int max = lookup.Count();
			foreach(int i in vertexes)
				lookup[i] = ++max;
			mesh.SetSharedIndexes(lookup);
		}

        /// <summary>
        /// Similar to Merge vertexes, expect that this method only collapses vertexes within a specified distance of one another (typically Mathf.Epsilon is used).
        /// </summary>
        /// <param name="mesh">Target pb_Object.</param>
        /// <param name="indexes">The vertex indexes to be scanned for inclusion. To weld the entire object for example, pass pb.faces.SelectMany(x => x.indexes).</param>
        /// <param name="neighborRadius">The minimum distance from another vertex to be considered within welding distance.</param>
        /// <returns>The indexes of any new vertexes created by a weld.</returns>
        public static int[] WeldVertexes(this ProBuilderMesh mesh, IEnumerable<int> indexes, float neighborRadius)
		{
            if (mesh == null)
                throw new ArgumentNullException("mesh");

            if (indexes == null)
                throw new ArgumentNullException("indexes");

            Vertex[] vertexes = Vertex.GetVertexes(mesh);
			IntArray[] sharedIndexes = mesh.sharedIndexesInternal;

			Dictionary<int, int> lookup = sharedIndexes.ToDictionary();
			HashSet<int> common = IntArrayUtility.GetCommonIndexes(lookup, indexes);
			int vertexCount = common.Count;

			// Make assumption that there will rarely be a time when a single weld encompasses more than 32 vertexes.
			// If a radial search returns neighbors matching the max count, the search is re-done and maxNearestNeighbors
			// is set to the resulting length. This will be slow, but in most cases shouldn't happen ever, or if it does,
			// should only happen once or twice.
			int maxNearestNeighbors = System.Math.Min(32, common.Count());

			// 3 dimensions, duplicate entries allowed
			KdTree<float, int> tree = new KdTree<float, int>(3, new FloatMath(), AddDuplicateBehavior.Collect);

			foreach(int i in common)
			{
				Vector3 v = vertexes[sharedIndexes[i][0]].position;
				tree.Add( new float[] { v.x, v.y, v.z }, i );
			}

			float[] point = new float[3] { 0, 0, 0 };
			Dictionary<int, int> remapped = new Dictionary<int, int>();
			Dictionary<int, Vector3> averages = new Dictionary<int, Vector3>();
			int index = sharedIndexes.Length;

			foreach(int commonIndex in common)
			{
				// already merged with another
				if(remapped.ContainsKey(commonIndex))
					continue;

				Vector3 v = vertexes[sharedIndexes[commonIndex][0]].position;

				point[0] = v.x;
				point[1] = v.y;
				point[2] = v.z;

				// Radial search at each point
				KdTreeNode<float, int>[] neighbors = tree.RadialSearch(point, neighborRadius, maxNearestNeighbors);

				// if first radial search filled the entire allotment reset the max neighbor count to 1.5x.
				// the result hopefully preventing double-searches in the next iterations.
				if(maxNearestNeighbors < vertexCount && neighbors.Length >= maxNearestNeighbors)
				{
					neighbors = tree.RadialSearch(point, neighborRadius, vertexCount);
					maxNearestNeighbors = System.Math.Min(vertexCount, neighbors.Length + neighbors.Length / 2);
				}

				Vector3 avg = Vector3.zero;
				float count = 0;

				for(int neighborIndex = 0; neighborIndex < neighbors.Length; neighborIndex++)
				{
					// common index of this neighbor
					int c = neighbors[neighborIndex].Value;

					// if it's already been added to another, skip it
					if(remapped.ContainsKey(c))
						continue;

					avg.x += neighbors[neighborIndex].Point[0];
					avg.y += neighbors[neighborIndex].Point[1];
					avg.z += neighbors[neighborIndex].Point[2];

					remapped.Add(c, index);

					count++;

					if(neighbors[neighborIndex].Duplicates != null)
					{
						for(int duplicateIndex = 0; duplicateIndex < neighbors[neighborIndex].Duplicates.Count; duplicateIndex++)
							remapped.Add(neighbors[neighborIndex].Duplicates[duplicateIndex], index);
					}
				}

				avg.x /= count;
				avg.y /= count;
				avg.z /= count;

				averages.Add(index, avg);

				index++;
			}

			var welds = new int[remapped.Count];
			int n = 0;

			foreach(var kvp in remapped)
			{
				int[] tris = sharedIndexes[kvp.Key];

				welds[n++] = tris[0];

				for(int i = 0; i < tris.Length; i++)
				{
					lookup[tris[i]] = kvp.Value;
					vertexes[tris[i]].position = averages[kvp.Value];
				}
			}

			mesh.SetSharedIndexes(lookup);
			mesh.SetVertexes(vertexes);
			mesh.ToMesh();
            return welds;
		}

		/// <summary>
		/// Split a common index on a face into two vertexes and slide each vertex backwards along it's feeding edge by distance.
		///	This method does not perform any input validation, so make sure edgeAndCommonIndex is distinct and all winged edges belong
		///	to the same face.
		///<pre>
		///	`appendedVertexes` is common index and a list of the new face indexes it was split into.
		///
		///	_ _ _ _          _ _ _
		///	|              /
		///	|         ->   |
		///	|              |
		/// </pre>
		/// </summary>
		/// <param name="vertexes"></param>
		/// <param name="edgeAndCommonIndex"></param>
		/// <param name="distance"></param>
		/// <param name="appendedVertexes"></param>
		/// <returns></returns>
		internal static FaceRebuildData ExplodeVertex(
			IList<Vertex> vertexes,
			IList<SimpleTuple<WingedEdge, int>> edgeAndCommonIndex,
			float distance,
			out Dictionary<int, List<int>> appendedVertexes)
		{
			Face face = edgeAndCommonIndex.FirstOrDefault().item1.face;
			List<Edge> perimeter = WingedEdge.SortEdgesByAdjacency(face);
			appendedVertexes = new Dictionary<int, List<int>>();
			Vector3 oldNormal = Math.Normal(vertexes, face.indexesInternal);

			// store local and common index of split points
			Dictionary<int, int> toSplit = new Dictionary<int, int>();

			foreach(SimpleTuple<WingedEdge, int> v in edgeAndCommonIndex)
			{
				if( v.item2 == v.item1.edge.common.a)
					toSplit.Add(v.item1.edge.local.a, v.item2);
				else
					toSplit.Add(v.item1.edge.local.b, v.item2);
			}

			int pc = perimeter.Count;
			List<Vertex> n_vertexes = new List<Vertex>();

			for(int i = 0; i < pc; i++)
			{
				int index = perimeter[i].b;

				// split this index into two
				if(toSplit.ContainsKey(index))
				{
					// a --- b --- c
					Vertex a = vertexes[perimeter[i].a];
					Vertex b = vertexes[perimeter[i].b];
					Vertex c = vertexes[perimeter[(i+1) % pc].b];

					Vertex leading_dir = a - b;
					Vertex following_dir = c - b;
					leading_dir.Normalize();
					following_dir.Normalize();

					Vertex leading_insert = vertexes[index] + leading_dir * distance;
					Vertex following_insert = vertexes[index] + following_dir * distance;

					appendedVertexes.AddOrAppend(toSplit[index], n_vertexes.Count);
					n_vertexes.Add(leading_insert);

					appendedVertexes.AddOrAppend(toSplit[index], n_vertexes.Count);
					n_vertexes.Add(following_insert);
				}
				else
				{
					n_vertexes.Add(vertexes[index]);
				}
			}

			List<int> triangles;

			if( Triangulation.TriangulateVertexes(n_vertexes, out triangles, false) )
			{
				FaceRebuildData data = new FaceRebuildData();
				data.vertexes = n_vertexes;
				data.face = new Face(face);

				Vector3 newNormal = Math.Normal(n_vertexes, triangles);

				if(Vector3.Dot(oldNormal, newNormal) < 0f)
					triangles.Reverse();

				data.face.indexesInternal = triangles.ToArray();

				return data;
			}

			return null;
		}

		static Edge AlignEdgeWithDirection(EdgeLookup edge, int commonIndex)
		{
			if(edge.common.a == commonIndex)
				return new Edge(edge.local.a, edge.local.b);
			else
				return new Edge(edge.local.b, edge.local.a);
		}
	}
}
