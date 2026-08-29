using UnityEngine;

namespace DLS.Description
{
	public struct NoteDescription
	{
		public int ID;
		public Vector2 Position;
		public string Text;
		public Vector2 Size;

		public NoteDescription(int id, Vector2 position, string text, Vector2 size)
		{
			ID = id;
			Position = position;
			Text = text;
			Size = size;
		}
	}
}