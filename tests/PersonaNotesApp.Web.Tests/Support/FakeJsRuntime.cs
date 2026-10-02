using Microsoft.JSInterop;

namespace PersonaNotesApp.Web.Tests.Support;

/// <summary>Minimal in-memory localStorage so auth code can run outside a browser.</summary>
public class FakeJsRuntime : IJSRuntime
{
    public Dictionary<string, string> Storage { get; } = new();

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        switch (identifier)
        {
            case "localStorage.getItem":
                Storage.TryGetValue((string)args![0]!, out var value);
                return new ValueTask<TValue>((TValue)(object?)value!);
            case "localStorage.setItem":
                Storage[(string)args![0]!] = (string)args[1]!;
                break;
            case "localStorage.removeItem":
                Storage.Remove((string)args![0]!);
                break;
        }

        return new ValueTask<TValue>(default(TValue)!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
        InvokeAsync<TValue>(identifier, args);
}
