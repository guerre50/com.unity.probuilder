﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEditor;
using UnityEngine;

namespace UnityEditor.ProBuilder
{
	/// <summary>
	/// Common troubleshooting actions for repairing ProBuilder meshes.
	/// </summary>
	/// @TODO MOVE THESE TO ACTIONS
	static class RepairActions
	{
		/// <summary>
		/// Menu interface for manually re-generating all ProBuilder geometry in scene.
		/// </summary>
		[MenuItem("Tools/" + PreferenceKeys.pluginTitle + "/Repair/Rebuild All ProBuilder Objects", false, PreferenceKeys.menuRepair)]
		public static void MenuForceSceneRefresh()
		{
			StringBuilder sb = new StringBuilder();
			ProBuilderMesh[] all = Object.FindObjectsOfType<ProBuilderMesh>();

			for (int i = 0, l = all.Length; i < l; i++)
			{
				UnityEditor.EditorUtility.DisplayProgressBar(
					"Refreshing ProBuilder Objects",
					"Reshaping pb_Object " + all[i].id + ".",
					((float) i / all.Length));

				try
				{
					all[i].ToMesh();
					all[i].Refresh();
					all[i].Optimize();
				}
				catch(System.Exception e)
				{
					if(!ReProBuilderize(all[i]))
						sb.AppendLine("Failed rebuilding: " + all[i].ToString() + "\n\t" + e.ToString());
				}
			}

			if(sb.Length > 0)
				Log.Error(sb.ToString());

			UnityEditor.EditorUtility.ClearProgressBar();
			UnityEditor.EditorUtility.DisplayDialog("Refresh ProBuilder Objects",
				"Successfully refreshed all ProBuilder objects in scene.",
				"Okay");
		}

		static bool ReProBuilderize(ProBuilderMesh pb)
		{
			try
			{
				GameObject go = pb.gameObject;
				pb.preserveMeshAssetOnDestroy = true;
				Undo.DestroyObjectImmediate(pb);

				// don't delete pb_Entity here because it won't
				// actually get removed till the next frame, and
				// probuilderize wants to add it if it's missing
				// (which it looks like it is from c# side but
				// is not)

				pb = Undo.AddComponent<ProBuilderMesh>(go);
				InternalMeshUtility.ResetPbObjectWithMeshFilter(pb, true);

				pb.ToMesh();
				pb.Refresh();
				pb.Optimize();

				return true;
			}
			catch
			{
				return false;
			}
		}

		[MenuItem("Tools/" + PreferenceKeys.pluginTitle + "/Repair/Rebuild Shared Indices Cache", true, PreferenceKeys.menuRepair)]
		static bool VertifyRebuildMeshes()
		{
			return InternalUtility.GetComponents<ProBuilderMesh>(Selection.transforms).Length > 0;
		}

		[MenuItem("Tools/" + PreferenceKeys.pluginTitle + "/Repair/Rebuild Shared Indices Cache", false, PreferenceKeys.menuRepair)]
		public static void DoRebuildMeshes()
		{
			RebuildSharedIndices( InternalUtility.GetComponents<ProBuilderMesh>(Selection.transforms) );
		}

		/// <summary>
		/// Rebuild targets if they can't be refreshed.
		/// </summary>
		/// <param name="targets"></param>
		static void RebuildSharedIndices(ProBuilderMesh[] targets)
		{
			StringBuilder sb = new StringBuilder();

			for(int i = 0; i < targets.Length; i++)
			{
				UnityEditor.EditorUtility.DisplayProgressBar(
					"Refreshing ProBuilder Objects",
					"Reshaping pb_Object " + targets[i].id + ".",
					((float)i / targets.Length));

				ProBuilderMesh pb = targets[i];

				try
				{
					pb.SetSharedIndices(IntArrayUtility.ExtractSharedIndices(pb.positions));

					pb.ToMesh();
					pb.Refresh();
					pb.Optimize();
				}
				catch(System.Exception e)
				{
					sb.AppendLine("Failed rebuilding " + pb.name + " shared indices cache.\n" + e.ToString());
				}
			}

			if(sb.Length > 0)
				Log.Error(sb.ToString());

			UnityEditor.EditorUtility.ClearProgressBar();
			UnityEditor.EditorUtility.DisplayDialog("Rebuild Shared Index Cache", "Successfully rebuilt " + targets.Length + " shared index caches", "Okay");
		}

		[MenuItem("Tools/" + PreferenceKeys.pluginTitle + "/Repair/Remove Degenerate Triangles", false, PreferenceKeys.menuRepair)]
		public static void MenuRemoveDegenerateTriangles()
		{
			int count = 0;

			foreach(ProBuilderMesh pb in InternalUtility.GetComponents<ProBuilderMesh>(Selection.transforms))
			{
				pb.ToMesh();

				int[] rm;
				pb.RemoveDegenerateTriangles(out rm);
				count += rm.Length;

				pb.ToMesh();
				pb.Refresh();
				pb.Optimize();
			}

			EditorUtility.ShowNotification("Removed " + (count/3) + " degenerate triangles.");
		}
	}
}