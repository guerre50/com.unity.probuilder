using UnityEngine;
using UnityEditor;
using ProBuilder2.Common;
using ProBuilder2.EditorCommon;
using ProBuilder2.Interface;
using System.Linq;

namespace ProBuilder2.Actions
{
	public class MergeFaces : pb_MenuAction
	{
		public override pb_IconGroup group { get { return pb_IconGroup.Geometry; } }
		public override Texture2D icon { get { return pb_IconUtility.GetIcon("Face_Merge"); } }
		public override pb_TooltipContent tooltip { get { return _tooltip; } }

		static readonly pb_TooltipContent _tooltip = new pb_TooltipContent
		(
			"Merge Faces",
			@"Tells ProBuilder to treat the selected faces as if they were a single face.  Be careful not to use this with unconnected faces!"
		);

		public override bool IsEnabled()
		{
			return 	pb_Editor.instance != null &&
					selection != null &&
					selection.Length > 0 &&
					selection.Any(x => x.SelectedFaceCount > 1);
		}

		public override bool IsHidden()
		{
			return 	pb_Editor.instance == null ||
					pb_Editor.instance.editLevel != EditLevel.Geometry ||
					pb_Editor.instance.selectionMode != SelectMode.Face;
		}

		public override pb_ActionResult DoAction()
		{
			return pb_Menu_Commands.MenuMergeFaces(selection);
		}
	}
}