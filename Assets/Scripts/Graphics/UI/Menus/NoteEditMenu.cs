using System;
using DLS.Game;
using Seb.Helpers;
using Seb.Types;
using Seb.Vis;
using Seb.Vis.UI;
using UnityEngine;
using static DLS.Graphics.DrawSettings;

namespace DLS.Graphics
{
	public static class NoteEditMenu
	{
		const float NoteTextFontSize = 3f;
		const float NoteLineSpacing = 1f;
		const float PadX = 3f;
		const float PadY = 3f;
		const float MinEditWidth = 30f;
		const float MaxEditWidth = 80f;
		const float MinEditHeight = 13f;
		const float MaxEditHeight = 26f;
		const float ButtonGroupHeight = 5.25f;
		const float ButtonGap = 1.2f;

		const float LineHeightMulti = 1.3f;

		const float RepeatStartDelay = 0.5f;
		const float RepeatDelay = 0.04f;
		static float backspaceLastManualTime;
		static float backspaceLastRepeatTime;

		static NoteInstance note;
		static string editText;
		static int cursorIndex;
		static bool hasFocus;
		static float scrollOffsetY;

		static readonly string[] CancelConfirmButtonNames = { "CANCEL", "CONFIRM" };
		static readonly bool[] ButtonGroupInteractStates = { true, true };

		public static void OnMenuOpened()
		{
			note = (NoteInstance)ContextMenu.interactionContext;
			editText = note.Text ?? string.Empty;
			cursorIndex = editText.Length;
			hasFocus = true;
			scrollOffsetY = 0;
		}

		public static void DrawMenu()
		{
			UI.DrawFullscreenPanel(ActiveUITheme.MenuBackgroundOverlayCol);

			Draw.ID panelID = UI.ReservePanel();

			using (UI.BeginBoundsScope(true))
			{
				Vector2 textSize = Draw.CalculateTextBoundsSize(editText, NoteTextFontSize, FontBold, NoteLineSpacing);
				float contentsHeight = textSize.y;
				Vector2 textAreaSize = new Vector2(
					Mathf.Clamp(textSize.x + PadX, MinEditWidth, MaxEditWidth),
					Mathf.Clamp(contentsHeight + PadY, MinEditHeight, MaxEditHeight)
				);
				float visibleHeight = textAreaSize.y - PadY;

				// Scroll bar state
				float maxScroll = Mathf.Max(0, contentsHeight - visibleHeight);
				scrollOffsetY = Mathf.Clamp(scrollOffsetY, 0, maxScroll);
				bool isScrollable = maxScroll > 0;

				// Centre the whole panel (text area + buttons) vertically
				float totalHeight = textAreaSize.y + ButtonGap + ButtonGroupHeight;
				float posYBottom = UI.Centre.y - totalHeight / 2;
				Vector2 bgPos = new(UI.Centre.x, posYBottom + textAreaSize.y / 2);

				UI.DrawPanel(bgPos, textAreaSize, new Color(0.15f, 0.15f, 0.15f));
				Bounds2D textAreaBounds = UI.PrevBounds;

				HandleTextInput(textAreaSize, bgPos, contentsHeight);

				// Draw scrolled text raw (in screen space) so it does not affect UI bounds/layout
				Vector2 textTopLeft = bgPos + new Vector2(-textAreaSize.x / 2 + PadX / 2, textAreaSize.y / 2 - PadY / 2);
				textTopLeft.y += scrollOffsetY;

				Bounds2D maskBounds = Bounds2D.CreateFromTopLeftAndSize(bgPos + new Vector2(-textAreaSize.x / 2, textAreaSize.y / 2), textAreaSize);
				using (UI.CreateMaskScope(maskBounds))
				{
					DrawTextRaw(editText, textTopLeft);
				}

				// Caret position indicator (does not affect layout)
				if (hasFocus)
				{
					DrawCaret(textTopLeft);
				}

				// Scroll indicator (does not affect layout)
				if (isScrollable)
				{
					DrawScrollIndicator(maskBounds, contentsHeight, visibleHeight);
				}

				Vector2 buttonsTopLeft = textAreaBounds.BottomLeft + Vector2.down * ButtonGap;
				float buttonWidth = Mathf.Max(textAreaBounds.Width, MinEditWidth);
				int buttonIndex = UI.HorizontalButtonGroup(CancelConfirmButtonNames, ButtonGroupInteractStates, ActiveUITheme.ButtonTheme, buttonsTopLeft, buttonWidth, DefaultButtonSpacing, 0, Anchor.TopLeft);

				MenuHelper.DrawReservedMenuPanel(panelID, UI.GetCurrentBoundsScope());

				if (KeyboardShortcuts.CancelShortcutTriggered || buttonIndex == 0) Cancel();
				else if (buttonIndex == 1) Confirm();
			}
		}

		// Draw text in screen space (does not register UI bounds, so it never affects layout)
		static void DrawTextRaw(string text, Vector2 textTopLeft)
		{
			float scale = GetUIScale();
			Vector2 pos_ss = UI.UIToScreenSpace(textTopLeft);
			Draw.Text(FontBold, text, NoteTextFontSize * scale, pos_ss, Anchor.TopLeft, Color.white, NoteLineSpacing);
		}

		static void DrawCaret(Vector2 textTopLeft)
		{
			float scale = GetUIScale();
			if (cursorIndex > 0 && cursorIndex < editText.Length && char.IsLowSurrogate(editText[cursorIndex]) && char.IsHighSurrogate(editText[cursorIndex - 1]))
			{
				// Skip drawing caret in the middle of a surrogate pair
				return;
			}

			// Find start of the current line and count the line index
			int lineStart = 0;
			int caretLineIndex = 0;
			for (int i = 0; i < cursorIndex; i++)
			{
				if (editText[i] == '\n')
				{
					lineStart = i + 1;
					caretLineIndex++;
				}
			}

			// X = width of the current line up to cursor (not the longest line)
			int len = cursorIndex - lineStart;
			float caretXText = len == 0 ? 0 : Draw.CalculateTextBoundsSize(editText.AsSpan(lineStart, len), NoteTextFontSize, FontBold, NoteLineSpacing).x;
			float lineHeight = NoteTextFontSize * NoteLineSpacing * LineHeightMulti;

			// Caret centre vertically on the current line, horizontally at cursor char
			float caretCentreYText = caretLineIndex * lineHeight + lineHeight / 2;
			Vector2 textTopLeft_ss = UI.UIToScreenSpace(textTopLeft);
			Vector2 caretPos_ss = textTopLeft_ss + new Vector2(caretXText * scale, -caretCentreYText * scale);
			Vector2 caretSize = new Vector2(0.04f, lineHeight * 0.9f) * scale;
			Draw.Quad(caretPos_ss, caretSize, Color.white);
		}

		// Draws a thin bar on the right edge showing scroll position (does not affect layout)
		static void DrawScrollIndicator(Bounds2D bounds, float contentsHeight, float visibleHeight)
		{
			float scale = GetUIScale();
			Bounds2D boundsSS = new(UI.UIToScreenSpace(bounds.Min), UI.UIToScreenSpace(bounds.Max));

			float barWidth = 0.4f * scale;
			float inset = 0.2f * scale;
			float xPos = boundsSS.Max.x - barWidth / 2 - inset;
			float trackHeight = boundsSS.Height - inset * 2;
			float trackTop = boundsSS.Max.y - inset;
			float trackBottom = trackTop - trackHeight;

			float t = contentsHeight > 0 ? scrollOffsetY / Mathf.Max(0.0001f, contentsHeight - visibleHeight) : 0;
			t = Mathf.Clamp01(t);

			// thumb size proportional to visible fraction
			float thumbSize = Mathf.Max(0.5f * scale, trackHeight * (visibleHeight / Mathf.Max(0.0001f, contentsHeight)));
			// thumb travels from top (t=0) to bottom (t=1)
			float thumbCentreY = trackTop - thumbSize / 2 - t * (trackHeight - thumbSize);

			Draw.Quad(new Vector2(xPos, (trackTop + trackBottom) / 2), new Vector2(barWidth, trackHeight), new Color(0, 0, 0, 0.25f));
			Draw.Quad(new Vector2(xPos, thumbCentreY), new Vector2(barWidth, thumbSize), new Color(1, 1, 1, 0.55f));
		}

		static float GetUIScale()
		{
			Vector2 one = UI.UIToScreenSpace(Vector2.one) - UI.UIToScreenSpace(Vector2.zero);
			return Mathf.Abs(one.x);
		}

		static void HandleTextInput(Vector2 textAreaSize, Vector2 bgPos, float contentsHeight)
		{
			float visibleHeight = textAreaSize.y - PadY;

			// Clamp scroll offset
			float maxScroll = Mathf.Max(0, contentsHeight - visibleHeight);
			scrollOffsetY = Mathf.Clamp(scrollOffsetY, 0, maxScroll);

			// Mouse wheel scrolling
			if (hasFocus)
			{
				scrollOffsetY -= InputHelper.MouseScrollDelta.y * NoteTextFontSize;
				scrollOffsetY = Mathf.Clamp(scrollOffsetY, 0, maxScroll);
			}

			Vector2 textAreaTopLeft = bgPos + new Vector2(-textAreaSize.x / 2, textAreaSize.y / 2);
			Bounds2D textBounds = Bounds2D.CreateFromTopLeftAndSize(textAreaTopLeft, textAreaSize);

			bool mouseInBounds = UI.MouseInsideBounds(textBounds);
			if (InputHelper.IsMouseDownThisFrame(MouseButton.Left))
			{
				hasFocus = mouseInBounds;
				if (hasFocus)
				{
					cursorIndex = CharIndexAtUIPos(InputHelper.MousePos, textAreaTopLeft);
				}
			}

			if (!hasFocus) return;

			foreach (char c in InputHelper.InputStringThisFrame)
			{
				if (char.IsSurrogate(c) || char.IsControl(c)) continue;
				editText = editText.Insert(cursorIndex, c.ToString());
				cursorIndex++;
			}

			if (InputHelper.IsKeyDownThisFrame(KeyCode.Return) || InputHelper.IsKeyDownThisFrame(KeyCode.KeypadEnter))
			{
				editText = editText.Insert(cursorIndex, "\n");
				cursorIndex++;
			}

			if (InputHelper.CtrlIsHeld && InputHelper.IsKeyDownThisFrame(KeyCode.V))
			{
				string clip = InputHelper.GetClipboardContents();
				if (!string.IsNullOrEmpty(clip))
				{
					editText = editText.Insert(cursorIndex, clip);
					cursorIndex += clip.Length;
				}
			}

			if (BackspaceTriggered() && cursorIndex > 0)
			{
				editText = editText.Remove(cursorIndex - 1, 1);
				cursorIndex--;
			}

			if (InputHelper.IsKeyDownThisFrame(KeyCode.Delete) && cursorIndex < editText.Length)
			{
				editText = editText.Remove(cursorIndex, 1);
			}

			if (InputHelper.IsKeyDownThisFrame(KeyCode.LeftArrow) && cursorIndex > 0)
				cursorIndex = PrevCharIndex();
			if (InputHelper.IsKeyDownThisFrame(KeyCode.RightArrow) && cursorIndex < editText.Length)
				cursorIndex = NextCharIndex();
			if (InputHelper.IsKeyDownThisFrame(KeyCode.UpArrow))
				cursorIndex = PrevLineIndex();
			if (InputHelper.IsKeyDownThisFrame(KeyCode.DownArrow))
				cursorIndex = NextLineIndex();
			if (InputHelper.IsKeyDownThisFrame(KeyCode.Home))
				cursorIndex = 0;
			if (InputHelper.IsKeyDownThisFrame(KeyCode.End))
				cursorIndex = editText.Length;

			KeepCaretVisible(textAreaSize, contentsHeight);
		}

		static void KeepCaretVisible(Vector2 textAreaSize, float contentsHeight)
		{
			float visibleHeight = textAreaSize.y - PadY;
			float maxScroll = Mathf.Max(0, contentsHeight - visibleHeight);

			// Get caret Y offset from top of text content (in UI units)
			if (cursorIndex <= 0)
			{
				scrollOffsetY = Mathf.Clamp(scrollOffsetY, 0, maxScroll);
				return;
			}

			Vector2 caretOffset = Draw.CalculateTextBoundsSize(editText.AsSpan(0, cursorIndex), NoteTextFontSize, FontBold, NoteLineSpacing);
			float caretY = caretOffset.y;

			if (caretY < scrollOffsetY) scrollOffsetY = caretY;
			else if (caretY > scrollOffsetY + visibleHeight) scrollOffsetY = caretY - visibleHeight;

			scrollOffsetY = Mathf.Clamp(scrollOffsetY, 0, maxScroll);
		}

		static bool BackspaceTriggered()
		{
			bool keyDown = InputHelper.IsKeyDownThisFrame(KeyCode.Backspace);
			bool keyHeld = InputHelper.IsKeyHeld(KeyCode.Backspace);

			if (keyDown)
			{
				backspaceLastManualTime = Time.time;
				backspaceLastRepeatTime = Time.time;
				return true;
			}

			if (keyHeld && Time.time - backspaceLastManualTime > RepeatStartDelay && Time.time - backspaceLastRepeatTime > RepeatDelay)
			{
				backspaceLastRepeatTime = Time.time;
				return true;
			}

			return false;
		}

		static int CharIndexAtUIPos(Vector2 screenPos, Vector2 textAreaTopLeft)
		{
			Vector2 textTopLeft_ss = UI.UIToScreenSpace(textAreaTopLeft + Vector2.up * scrollOffsetY);
			Vector2 scaleVec = UI.UIToScreenSpace(textAreaTopLeft + Vector2.one) - UI.UIToScreenSpace(textAreaTopLeft);
			float scale = scaleVec.y;

			float lineHeight = NoteTextFontSize * scale * NoteLineSpacing * LineHeightMulti;
			int lineIndex = Mathf.FloorToInt((textTopLeft_ss.y - screenPos.y) / lineHeight);
			lineIndex = Mathf.Clamp(lineIndex, 0, CountLines() - 1);

			int lineStart = GetLineStartIndex(lineIndex);
			int lineEnd = GetLineEndIndex(lineIndex);
			if (lineStart >= lineEnd) return lineStart;

			float bestDst = float.MaxValue;
			int bestIndex = lineEnd;
			for (int i = lineStart; i <= lineEnd; i++)
			{
				int len = i - lineStart;
				float charX = len == 0 ? 0 : Draw.CalculateTextBoundsSize(editText.AsSpan(lineStart, len), NoteTextFontSize, FontBold, NoteLineSpacing).x * scale;
				float dst = Mathf.Abs(charX - (screenPos.x - textTopLeft_ss.x));
				if (dst < bestDst)
				{
					bestDst = dst;
					bestIndex = i;
				}
			}

			return bestIndex;
		}

		static int PrevCharIndex()
		{
			return cursorIndex > 0 ? cursorIndex - 1 : 0;
		}

		static int NextCharIndex()
		{
			return cursorIndex < editText.Length ? cursorIndex + 1 : editText.Length;
		}

		static int PrevLineIndex()
		{
			int currLineStart = GetLineStartIndex(GetLineAtCharIndex(cursorIndex));
			if (cursorIndex == 0) return 0;

			int prevLineEnd = cursorIndex - 1;
			while (prevLineEnd > 0 && editText[prevLineEnd - 1] != '\n') prevLineEnd--;
			int prevLineStart = GetLineStartIndex(GetLineAtCharIndex(prevLineEnd));

			int colInCurrLine = cursorIndex - currLineStart;
			return Mathf.Min(prevLineStart + colInCurrLine, prevLineEnd);
		}

		static int NextLineIndex()
		{
			if (cursorIndex >= editText.Length) return editText.Length;

			int currLineStart = GetLineStartIndex(GetLineAtCharIndex(cursorIndex));
			int currLineEnd = GetLineEndIndex(GetLineAtCharIndex(cursorIndex));
			if (currLineEnd >= editText.Length) return editText.Length;

			int nextLineStart = currLineEnd + 1;
			int nextLineEnd = GetLineEndIndex(GetLineAtCharIndex(nextLineStart));

			int colInCurrLine = cursorIndex - currLineStart;
			return Mathf.Min(nextLineStart + colInCurrLine, nextLineEnd);
		}

		static int GetLineStartIndex(int lineIndex)
		{
			int idx = 0;
			int currentLine = 0;
			while (currentLine < lineIndex && idx < editText.Length)
			{
				if (editText[idx] == '\n') currentLine++;
				idx++;
			}
			return idx;
		}

		static int GetLineEndIndex(int lineIndex)
		{
			int idx = GetLineStartIndex(lineIndex);
			while (idx < editText.Length && editText[idx] != '\n') idx++;
			return idx;
		}

		static int CountLines()
		{
			if (string.IsNullOrEmpty(editText)) return 1;
			int count = 1;
			for (int i = 0; i < editText.Length; i++)
			{
				if (editText[i] == '\n') count++;
			}
			return count;
		}

		static int GetLineAtCharIndex(int charIdx)
		{
			if (charIdx <= 0) return 0;
			int line = 0;
			for (int i = 0; i < charIdx && i < editText.Length; i++)
			{
				if (editText[i] == '\n') line++;
			}
			return line;
		}

		static void Confirm()
		{
			note.Text = editText;
			note.Size = note.AutoSize;
			UIDrawer.SetActiveMenu(UIDrawer.MenuType.None);
		}

		static void Cancel()
		{
			UIDrawer.SetActiveMenu(UIDrawer.MenuType.None);
		}
	}
}