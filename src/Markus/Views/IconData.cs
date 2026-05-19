using Avalonia.Media;

namespace Markus.Views;

// Material Design Icons (Apache 2.0 license, https://pictogrammers.com/library/mdi/).
// Single 24x24 viewBox path each, used through `<PathIcon Data="{x:Static …}"/>`.
internal static class IconData
{
    public static StreamGeometry Folder { get; } =
        StreamGeometry.Parse(
            "M10,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V8C22,6.89 21.1,6 20,6H12L10,4Z"
        );

    public static StreamGeometry Refresh { get; } =
        StreamGeometry.Parse(
            "M17.65,6.35C16.2,4.9 14.21,4 12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20C15.73,20 18.84,17.45 19.73,14H17.65C16.83,16.33 14.61,18 12,18A6,6 0 0,1 6,12A6,6 0 0,1 12,6C13.66,6 15.14,6.69 16.22,7.78L13,11H20V4L17.65,6.35Z"
        );

    public static StreamGeometry Sidebar { get; } =
        StreamGeometry.Parse("M3,3V21H21V3H3M5,5H10V19H5V5M12,5H19V19H12V5Z");

    public static StreamGeometry Settings { get; } =
        StreamGeometry.Parse(
            "M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12C19.5,11.67 19.47,11.34 19.43,11L21.54,9.37C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.96 19.05,5.05L16.56,6.05C16.04,5.66 15.5,5.32 14.87,5.07L14.5,2.42C14.46,2.18 14.25,2 14,2H10C9.75,2 9.54,2.18 9.5,2.42L9.13,5.07C8.5,5.32 7.96,5.66 7.44,6.05L4.95,5.05C4.72,4.96 4.46,5.05 4.34,5.27L2.34,8.73C2.21,8.95 2.27,9.22 2.46,9.37L4.57,11C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,12.97L2.46,14.63C2.27,14.78 2.21,15.05 2.34,15.27L4.34,18.73C4.46,18.95 4.73,19.03 4.95,18.95L7.44,17.94C7.96,18.34 8.5,18.68 9.13,18.93L9.5,21.58C9.54,21.82 9.75,22 10,22H14C14.25,22 14.46,21.82 14.5,21.58L14.87,18.93C15.5,18.67 16.04,18.34 16.56,17.94L19.05,18.95C19.27,19.03 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63L19.43,12.97Z"
        );

    public static StreamGeometry Palette { get; } =
        StreamGeometry.Parse(
            "M17.5,12A1.5,1.5 0 0,1 16,10.5A1.5,1.5 0 0,1 17.5,9A1.5,1.5 0 0,1 19,10.5A1.5,1.5 0 0,1 17.5,12M14.5,8A1.5,1.5 0 0,1 13,6.5A1.5,1.5 0 0,1 14.5,5A1.5,1.5 0 0,1 16,6.5A1.5,1.5 0 0,1 14.5,8M9.5,8A1.5,1.5 0 0,1 8,6.5A1.5,1.5 0 0,1 9.5,5A1.5,1.5 0 0,1 11,6.5A1.5,1.5 0 0,1 9.5,8M6.5,12A1.5,1.5 0 0,1 5,10.5A1.5,1.5 0 0,1 6.5,9A1.5,1.5 0 0,1 8,10.5A1.5,1.5 0 0,1 6.5,12M12,3A9,9 0 0,0 3,12A9,9 0 0,0 12,21A1.5,1.5 0 0,0 13.5,19.5C13.5,19.11 13.35,18.76 13.11,18.5C12.88,18.23 12.73,17.88 12.73,17.5A1.5,1.5 0 0,1 14.23,16H16A5,5 0 0,0 21,11C21,6.58 16.97,3 12,3M12,5C15.87,5 19,7.69 19,11A3,3 0 0,1 16,14H14.23A3.5,3.5 0 0,0 10.73,17.5C10.73,17.96 10.81,18.41 10.95,18.83C7.05,18.34 5,15.43 5,12A7,7 0 0,1 12,5Z"
        );

    public static StreamGeometry ViewQuilt { get; } =
        StreamGeometry.Parse(
            "M10,18H3V13H10V18M10,11H3V6H10V11M11,11H21V6H11V11M11,18H21V13H11V18M9,17V14H4V17H9M9,10V7H4V10H9M12,17H20V14H12V17M12,10H20V7H12V10Z"
        );

    public static StreamGeometry Tune { get; } =
        StreamGeometry.Parse(
            "M3,17V19H9V17H3M3,5V7H13V5H3M13,21V19H21V17H13V15H11V21H13M7,9V11H3V13H7V15H9V9H7M21,13V11H11V13H21M15,9H17V7H21V5H17V3H15V9Z"
        );

    public static StreamGeometry UnfoldMore { get; } =
        StreamGeometry.Parse(
            "M12,18.17L8.83,15L7.41,16.41L12,21L16.59,16.41L15.17,15L12,18.17M12,5.83L15.17,9L16.58,7.59L12,3L7.41,7.59L8.83,9L12,5.83Z"
        );

    public static StreamGeometry UnfoldLess { get; } =
        StreamGeometry.Parse(
            "M7.41,18.59L8.83,20L12,16.83L15.17,20L16.58,18.59L12,14L7.41,18.59M16.59,5.41L15.17,4L12,7.17L8.83,4L7.41,5.41L12,10L16.59,5.41Z"
        );

    public static StreamGeometry FolderOpen { get; } =
        StreamGeometry.Parse(
            "M19,20H4C2.89,20 2,19.1 2,18V6C2,4.89 2.89,4 4,4H10L12,6H19A2,2 0 0,1 21,8H21L4,8V18L6.14,10H23.21L20.93,18.5C20.7,19.37 19.92,20 19,20Z"
        );

    public static StreamGeometry ChevronDown { get; } =
        StreamGeometry.Parse("M7.41,8.58L12,13.17L16.59,8.58L18,10L12,16L6,10L7.41,8.58Z");

    public static StreamGeometry History { get; } =
        StreamGeometry.Parse(
            "M13.5,8H12V13L16.28,15.54L17,14.33L13.5,12.25V8M13,3A9,9 0 0,0 4,12H1L4.96,16.03L9,12H6A7,7 0 0,1 13,5A7,7 0 0,1 20,12A7,7 0 0,1 13,19C11.07,19 9.32,18.21 8.06,16.94L6.64,18.36C8.27,20 10.5,21 13,21A9,9 0 0,0 22,12A9,9 0 0,0 13,3"
        );

    public static StreamGeometry ArrowUp { get; } =
        StreamGeometry.Parse("M13,20H11V8L5.5,13.5L4.08,12.08L12,4.16L19.92,12.08L18.5,13.5L13,8V20Z");

    public static StreamGeometry ArrowDown { get; } =
        StreamGeometry.Parse("M11,4H13V16L18.5,10.5L19.92,11.92L12,19.84L4.08,11.92L5.5,10.5L11,16V4Z");

    public static StreamGeometry Close { get; } =
        StreamGeometry.Parse(
            "M19,6.41L17.59,5L12,10.59L6.41,5L5,6.41L10.59,12L5,17.59L6.41,19L12,13.41L17.59,19L19,17.59L13.41,12L19,6.41Z"
        );

    public static StreamGeometry Lock { get; } =
        StreamGeometry.Parse(
            "M12,17A2,2 0 0,0 14,15C14,13.89 13.1,13 12,13A2,2 0 0,0 10,15A2,2 0 0,0 12,17M18,8A2,2 0 0,1 20,10V20A2,2 0 0,1 18,22H6A2,2 0 0,1 4,20V10C4,8.89 4.9,8 6,8H7V6A5,5 0 0,1 12,1A5,5 0 0,1 17,6V8H18M12,3A3,3 0 0,0 9,6V8H15V6A3,3 0 0,0 12,3Z"
        );
}
