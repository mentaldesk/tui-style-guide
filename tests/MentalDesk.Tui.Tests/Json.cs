using System.Text.Encodings.Web;
using System.Text.Json;

namespace MentalDesk.Tui.Tests;

internal static class Json
{
    public static JsonSerializerOptions Readable { get; } = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
}
