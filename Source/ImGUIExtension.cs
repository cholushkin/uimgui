using ImGuiNET;


namespace UImGui 
{
    public static class ImGUIExtension // todo: rename class (there is with the same name in in utils )
    {
        public static void HelpMarker(string description)
        {
            ImGui.TextDisabled("[?]");
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
                ImGui.TextUnformatted(description);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
        }
    }
}