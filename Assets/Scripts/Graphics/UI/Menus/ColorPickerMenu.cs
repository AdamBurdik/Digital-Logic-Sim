using System.Collections.Generic;
using DLS.Game;
using Seb.Helpers;
using Seb.Types;
using Seb.Vis;
using Seb.Vis.UI;
using UnityEngine;

namespace DLS.Graphics
{
	public static class ColorPickerMenu
	{
		static readonly UIHandle ID_ColourPicker = new("PinColourPicker_Col");

		const float PanelWidth = 34f;
		const float PanelPadding = 1.5f;
		const float ContentWidth = PanelWidth - PanelPadding * 2;
		const float RecentSwatchSize = 2.5f;
		const float RecentSwatchSpacing = 0.5f;
		const int RecentColoursPerRow = 4;

		public static void OnMenuOpened()
		{
			// Seed the picker with the pin's current colour
			Color currentCol = GetCurrentColour();
			ColourPickerState pickerState = UI.GetColourPickerState(ID_ColourPicker);
			Color.RGBToHSV(currentCol, out pickerState.hue, out pickerState.sat, out pickerState.val);
		}

		public static void DrawMenu()
		{
			MenuHelper.DrawBackgroundOverlay();
			Draw.ID panelID = UI.ReservePanel();

			using (UI.BeginBoundsScope(true))
			{
				Vector2 centre = UI.Centre;
				float shiftUp = 100f / GetUIScale(); // shift menu up by ~100px
				Vector2 topLeft = centre + new Vector2(-PanelWidth / 2, 9 + shiftUp);

				// ---- Cancel / Confirm buttons (top) ----
				MenuHelper.CancelConfirmResult result = MenuHelper.DrawCancelConfirmButtons(topLeft, ContentWidth, false);
				Vector2 buttonsBottomLeft = UI.PrevBounds.BottomLeft;

				// ---- Colour picker ----
				Vector2 pickerTopLeft = buttonsBottomLeft + Vector2.down * 1f;
				Color newCol = UI.DrawColourPicker(ID_ColourPicker, pickerTopLeft, ContentWidth, Anchor.TopLeft);
				Vector2 pickerBottomLeft = UI.PrevBounds.BottomLeft;

				// ---- Recent colours label ----
				Vector2 recentLabelPos = pickerBottomLeft + Vector2.down;
				MenuHelper.DrawText("RECENT", recentLabelPos, Anchor.TopLeft, Color.white, true);
				Vector2 recentGridTopLeft = UI.PrevBounds.BottomLeft + Vector2.down;

				// ---- Recent colours grid ----
				DrawRecentColoursGrid(recentGridTopLeft);

				Bounds2D bounds = UI.GetCurrentBoundsScope();
				MenuHelper.DrawReservedMenuPanel(panelID, bounds);

				if (result == MenuHelper.CancelConfirmResult.Cancel) Cancel();
				else if (result == MenuHelper.CancelConfirmResult.Confirm) Confirm(newCol);
			}
		}

		static void DrawRecentColoursGrid(Vector2 topLeft)
		{
			List<Color> recentColours = Project.ActiveProject.description.RecentPinColours;
			int numRecent = recentColours?.Count ?? 0;
			if (numRecent == 0)
			{
				MenuHelper.DrawText("(None yet)", topLeft, Anchor.TopLeft, new Color(1, 1, 1, 0.5f));
				return;
			}

			for (int i = 0; i < numRecent; i++)
			{
				int row = i / RecentColoursPerRow;
				int colInRow = i % RecentColoursPerRow;
				Vector2 swatchCentre = topLeft + new Vector2(
					RecentSwatchSize / 2 + colInRow * (RecentSwatchSize + RecentSwatchSpacing),
					-(RecentSwatchSize / 2 + row * (RecentSwatchSize + RecentSwatchSpacing))
				);

				Color col = recentColours[i];
				Bounds2D swatchBounds = Bounds2D.CreateFromCentreAndSize(swatchCentre, Vector2.one * RecentSwatchSize);
				bool mouseOver = UI.MouseInsideBounds(swatchBounds);
				Color swatchCol = mouseOver ? ColHelper.Brighten(col, 0.1f) : col;

				UI.DrawPanel(swatchCentre, Vector2.one * RecentSwatchSize, swatchCol);

				// Outline (raw draw, so convert to screen space)
				Vector2 swatchCentre_ss = UI.UIToScreenSpace(swatchCentre);
				float size_ss = RecentSwatchSize * GetUIScale();
				Draw.QuadOutline(swatchCentre_ss, Vector2.one * (size_ss + 0.1f), 0.03f, new Color(1, 1, 1, 0.25f));

				if (mouseOver && InputHelper.IsMouseDownThisFrame(MouseButton.Left))
				{
					ColourPickerState pickerState = UI.GetColourPickerState(ID_ColourPicker);
					pickerState.SetRGB(col);
				}
			}
		}

		static float GetUIScale()
		{
			Vector2 one = UI.UIToScreenSpace(Vector2.one) - UI.UIToScreenSpace(Vector2.zero);
			return Mathf.Abs(one.x);
		}

		static void Confirm(Color newCol)
		{
			ContextMenu.SetCustomColour(newCol);
			Close();
		}

		static void Cancel()
		{
			Close();
		}

		static void Close()
		{
			UIDrawer.SetActiveMenu(UIDrawer.MenuType.None);
		}

		static Color GetCurrentColour()
		{
			if (ContextMenu.interactionContext is PinInstance pin)
			{
				return pin.CustomColour ?? pin.GetColHigh();
			}

			return Color.white;
		}
	}
}