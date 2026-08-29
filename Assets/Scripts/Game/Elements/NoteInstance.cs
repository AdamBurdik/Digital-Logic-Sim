using System;
using DLS.Description;
using DLS.Graphics;
using Seb.Helpers;
using Seb.Types;
using Seb.Vis;
using UnityEngine;
using static DLS.Graphics.DrawSettings;

namespace DLS.Game
{
	public class NoteInstance : IMoveable
	{
		public const float NotePaddingX = 0.11f;
		public const float NotePaddingY = 0.075f;
		public const float NoteLineSpacing = 0.8f;
		public const float NoteFontSize = 0.16f;
		public static readonly Vector2 MinNoteSize = new(GridSize * 3, GridSize * 2.5f);

		public string Text;
		public Vector2 Size;

		public NoteInstance(int id, Vector2 position, string text, Vector2 size)
		{
			ID = id;
			Position = position;
			Text = text ?? string.Empty;
			Size = Vector2.Max(size, MinNoteSize);
		}

		public Vector2 Position { get; set; }
		public Vector2 MoveStartPosition { get; set; }
		public Vector2 StraightLineReferencePoint { get; set; }
		public int ID { get; }
		public bool IsSelected { get; set; }
		public bool HasReferencePointForStraightLineMovement { get; set; }
		public bool IsValidMovePos { get; set; }

		public bool ShouldBeIncludedInSelectionBox(Vector2 selectionCentre, Vector2 selectionSize) =>
			Maths.PointInBox2D(Position, selectionCentre, selectionSize);

		public Bounds2D SelectionBoundingBox => CreateBoundingBox(SelectionBoundsPadding);
		public Bounds2D BoundingBox => CreateBoundingBox(0);

		public Vector2 SnapPoint => Position;

		public Vector2 AutoSize
		{
			get
			{
				if (string.IsNullOrEmpty(Text)) return MinNoteSize;
				Vector2 textSize = Draw.CalculateTextBoundsSize(Text, NoteFontSize, FontBold, NoteLineSpacing);
				return Vector2.Max(textSize + new Vector2(NotePaddingX * 2, NotePaddingY * 2), MinNoteSize);
			}
		}

		Bounds2D CreateBoundingBox(float pad)
		{
			Vector2 s = Size + Vector2.one * pad;
			return Bounds2D.CreateFromCentreAndSize(Position, s);
		}
	}
}