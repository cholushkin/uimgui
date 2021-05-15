﻿using AOT;
using ImGuiNET;
using NumericsConverter;
using System;
using UImGui.Assets;
using UnityEngine;
using UnityEngine.Assertions;

namespace UImGui.Platform
{
	// TODO: Check this feature and remove from here when checked and done.
	// Implemented features:
	// [x] Platform: Clipboard support.
	// [x] Platform: Mouse cursor shape and visibility. Disable with io.ConfigFlags |= ImGuiConfigFlags.NoMouseCursorChange.
	// [x] Platform: Keyboard arrays indexed using KeyCode codes, e.g. ImGui.IsKeyPressed(KeyCode.Space).
	// [ ] Platform: Gamepad support. Enabled with io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad.
	// [~] Platform: IME support.
	// [~] Platform: INI settings support.

	/// <summary>
	/// Platform bindings for ImGui in Unity in charge of: mouse/keyboard/gamepad inputs, cursor shape, timing, windowing.
	/// </summary>
	internal sealed class InputManagerPlatform : IPlatform
	{
		private readonly CursorShapesAsset _cursorShapes;
		private readonly IniSettingsAsset _iniSettings;
		private readonly Event _textInputEvent = new Event();
		private readonly PlatformCallbacks _callbacks = new PlatformCallbacks();

		private int[] _mainKeys;
		private ImGuiMouseCursor _lastCursor = ImGuiMouseCursor.COUNT;

		public InputManagerPlatform(CursorShapesAsset cursorShapes, IniSettingsAsset iniSettings)
		{
			_cursorShapes = cursorShapes;
			_iniSettings = iniSettings;
		}

		[MonoPInvokeCallback(typeof(GetClipboardTextCallback))]
		public static unsafe string GetClipboardTextCallback(void* user_data)
		{
			return GUIUtility.systemCopyBuffer;
		}

		[MonoPInvokeCallback(typeof(SetClipboardTextCallback))]
		public static unsafe void SetClipboardTextCallback(void* user_data, byte* text)
		{
			GUIUtility.systemCopyBuffer = Utils.StringFromPtr(text);
		}

		[MonoPInvokeCallback(typeof(ImeSetInputScreenPosCallback))]
		public static unsafe void ImeSetInputScreenPosCallback(int x, int y)
		{
			Input.compositionCursorPos = new Vector2(x, y);
		}

		// TODO: Implement (check) Log.
#if IMGUI_FEATURE_CUSTOM_ASSERT
		//[MonoPInvokeCallback(typeof(LogAssertCallback))]
		//public static unsafe void LogAssertCallback(byte* condition, byte* file, int line)
		//{
		//	Debug.LogError($"[DearImGui] Assertion failed: '{Util.StringFromPtr(condition)}', file '{Util.StringFromPtr(file)}', line: {line}.");
		//}

		//[MonoPInvokeCallback(typeof(DebugBreakCallback))]
		//public static unsafe void DebugBreakCallback()
		//{
		//	System.Diagnostics.Debugger.Break();
		//}
#endif

		public bool Initialize(ImGuiIOPtr io, UIOConfig config)
		{
			io.SetBackendPlatformName("Unity Input Manager");
			io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;

			// TODO: Check if this works
			if ((config.ImGuiConfig & ImGuiConfigFlags.NavEnableSetMousePos) != 0)
			{
				io.BackendFlags |= ImGuiBackendFlags.HasSetMousePos;
				io.WantSetMousePos = true;
			}
			else
			{
				io.BackendFlags &= ~ImGuiBackendFlags.HasSetMousePos;
				io.WantSetMousePos = false;
			}

			unsafe
			{
				// TODO: Implement (check) this.
#if IMGUI_FEATURE_CUSTOM_ASSERT
				//PlatformCallbacks.SetClipboardFunctions( 
				//	GetClipboardTextCallback, SetClipboardTextCallback,
				//	LogAssertCallback, DebugBreakCallback);
#else
				PlatformCallbacks.SetClipboardFunctions(GetClipboardTextCallback, SetClipboardTextCallback);
#endif
			}

			io.ClipboardUserData = IntPtr.Zero;

			if (_iniSettings != null)
			{
				io.SetIniFilename(null);
				ImGui.LoadIniSettingsFromMemory(_iniSettings.Load());
			}

			SetupKeyboard(io);

			_callbacks.Assign(io);

			return true;
		}

		public void Shutdown(ImGuiIOPtr io)
		{
			io.SetBackendPlatformName(null);

			_callbacks.Unset(io);
		}

		public void PrepareFrame(ImGuiIOPtr io, Rect displayRect)
		{
			Assert.IsTrue(io.Fonts.IsBuilt(), "Font atlas not built! Generally built by the renderer. Missing call to renderer NewFrame() function?");

			io.DisplaySize = displayRect.size.ToSystem(); // TODO: dpi aware, scale, etc

			io.DeltaTime = Time.unscaledDeltaTime;

			UpdateKeyboard(io);
			UpdateMouse(io);
			UpdateCursor(io, ImGui.GetMouseCursor());

			if (_iniSettings != null && io.WantSaveIniSettings)
			{
				_iniSettings.Save(ImGui.SaveIniSettingsToMemory());
				io.WantSaveIniSettings = false;
			}
		}

		private void SetupKeyboard(ImGuiIOPtr io)
		{
			// Map and store new keys by assigning io.KeyMap and setting value of array
			_mainKeys = new int[] {
				io.KeyMap[(int)ImGuiKey.A] = (int)KeyCode.A, // For text edit CTRL+A: select all.
				io.KeyMap[(int)ImGuiKey.C] = (int)KeyCode.C, // For text edit CTRL+C: copy.
				io.KeyMap[(int)ImGuiKey.V] = (int)KeyCode.V, // For text edit CTRL+V: paste.
				io.KeyMap[(int)ImGuiKey.X] = (int)KeyCode.X, // For text edit CTRL+X: cut.
				io.KeyMap[(int)ImGuiKey.Y] = (int)KeyCode.Y, // For text edit CTRL+Y: redo.
				io.KeyMap[(int)ImGuiKey.Z] = (int)KeyCode.Z, // For text edit CTRL+Z: undo.

				io.KeyMap[(int)ImGuiKey.Tab] = (int)KeyCode.Tab,

				io.KeyMap[(int)ImGuiKey.LeftArrow] = (int)KeyCode.LeftArrow,
				io.KeyMap[(int)ImGuiKey.RightArrow] = (int)KeyCode.RightArrow,
				io.KeyMap[(int)ImGuiKey.UpArrow] = (int)KeyCode.UpArrow,
				io.KeyMap[(int)ImGuiKey.DownArrow] = (int)KeyCode.DownArrow,

				io.KeyMap[(int)ImGuiKey.PageUp] = (int)KeyCode.PageUp,
				io.KeyMap[(int)ImGuiKey.PageDown] = (int)KeyCode.PageDown,

				io.KeyMap[(int)ImGuiKey.Home] = (int)KeyCode.Home,
				io.KeyMap[(int)ImGuiKey.End] = (int)KeyCode.End,
				io.KeyMap[(int)ImGuiKey.Insert] = (int)KeyCode.Insert,
				io.KeyMap[(int)ImGuiKey.Delete] = (int)KeyCode.Delete,

				io.KeyMap[(int)ImGuiKey.Backspace] = (int)KeyCode.Backspace,
				io.KeyMap[(int)ImGuiKey.Space] = (int)KeyCode.Space,
				io.KeyMap[(int)ImGuiKey.Escape] = (int)KeyCode.Escape,
				io.KeyMap[(int)ImGuiKey.Enter] = (int)KeyCode.Return,
				io.KeyMap[(int)ImGuiKey.KeyPadEnter] = (int)KeyCode.KeypadEnter,
			};
		}

		private void UpdateKeyboard(ImGuiIOPtr io)
		{
			for (int keyIndex = 0; keyIndex < _mainKeys.Length; keyIndex++)
			{
				int key = _mainKeys[keyIndex];
				io.KeysDown[key] = Input.GetKey((KeyCode)key);
			}

			// Keyboard modifiers.
			io.KeyShift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
			io.KeyCtrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
			io.KeyAlt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
			io.KeySuper = Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand)
				|| Input.GetKey(KeyCode.LeftWindows) || Input.GetKey(KeyCode.RightWindows);

			// text input
			while (Event.PopEvent(_textInputEvent))
			{
				if (_textInputEvent.rawType == EventType.KeyDown &&
					_textInputEvent.character != 0 && _textInputEvent.character != '\n')
				{
					io.AddInputCharacter(_textInputEvent.character);
				}
			}
		}

		private static void UpdateMouse(ImGuiIOPtr io)
		{
			io.MousePos = Utils.ScreenToImGui(Input.mousePosition);

			io.MouseWheel = Input.mouseScrollDelta.y;
			io.MouseWheelH = Input.mouseScrollDelta.x;

			io.MouseDown[0] = Input.GetMouseButton(0);
			io.MouseDown[1] = Input.GetMouseButton(1);
			io.MouseDown[2] = Input.GetMouseButton(2);
		}

		private void UpdateCursor(ImGuiIOPtr io, ImGuiMouseCursor cursor)
		{
			if (_lastCursor == cursor) return;
			if ((io.ConfigFlags & ImGuiConfigFlags.NoMouseCursorChange) != 0) return;

			if (io.MouseDrawCursor)
			{
				cursor = ImGuiMouseCursor.None;
			}

			_lastCursor = cursor;
			Cursor.visible = cursor != ImGuiMouseCursor.None;

			if (Cursor.visible && _cursorShapes != null)
			{
				Cursor.SetCursor(_cursorShapes[cursor].Texture, _cursorShapes[cursor].Hotspot, CursorMode.Auto);
			}
		}
	}
}