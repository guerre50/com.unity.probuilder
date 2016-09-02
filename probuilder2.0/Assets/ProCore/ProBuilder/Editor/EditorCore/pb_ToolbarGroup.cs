using UnityEngine;
using UnityEditor;

namespace ProBuilder2.EditorCommon
{
	[System.Obsolete("Use pb_ToolbarGroup instead")]
	public enum pb_IconGroup
	{
		Tool		= 0,
		Selection	= 1,
		Object		= 2,
		Geometry	= 3,
		Entity		= 4,
		Export 		= 5
	}

	/**
	 *	Defines what area of the ProBuilder toolbar a pb_MenuAction should be grouped into.
	 */
	public enum pb_ToolbarGroup
	{
		Tool		= 0,
		Selection	= 1,
		Object		= 2,
		Geometry	= 3,
		Entity		= 4,
		Export 		= 5
	}

	public static class pb_ToolbarGroupUtility
	{
		static readonly Color ToolColor 		= new Color(0.6666f, 0.4f, 0.2f, 1f);
		static readonly Color SelectionColor 	= new Color(0.1411f, 0.4941f, 0.6392f, 1f);
		static readonly Color ObjectColor 		= new Color(0.4f, 0.6f, 0.1333f, 1f);
		static readonly Color GeometryColor		= new Color(0.7333f, 0.1333f, 0.2f, 1f);

		public static Color GetColor(pb_ToolbarGroup group)
		{
			if( group == pb_ToolbarGroup.Tool )
				return ToolColor;
			else if( group == pb_ToolbarGroup.Selection )
				return SelectionColor;
			else if( group == pb_ToolbarGroup.Object || group == pb_ToolbarGroup.Entity )
				return ObjectColor;
			else if( group == pb_ToolbarGroup.Geometry )
				return GeometryColor;

			return Color.white;
		}
	}
}