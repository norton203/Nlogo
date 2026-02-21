namespace Nlogo.Compiler;

/// <summary>
/// A linked chain of variable scopes.
/// Each procedure call gets its own scope that shadows the global one.
/// </summary>
public sealed class LogoEnvironment
{
    private readonly Dictionary<string, object?> _vars = new();
    private readonly LogoEnvironment? _parent;

    public LogoEnvironment(LogoEnvironment? parent = null)
        => _parent = parent;

    // ── Read ───────────────────────────────────────────────────────
    public object? Get(string name)
    {
        string key = name.ToUpperInvariant();

        if (_vars.TryGetValue(key, out var val))
            return val;

        if (_parent is not null)
            return _parent.Get(key);

        throw new RuntimeException($"Variable '{name}' is not defined");
    }

    // ── Write ──────────────────────────────────────────────────────

    /// Set in whichever scope already owns this variable (or global if none).
    public void Set(string name, object? value)
    {
        string key = name.ToUpperInvariant();

        if (_vars.ContainsKey(key))
        {
            _vars[key] = value;
            return;
        }

        if (_parent is not null && _parent.Has(key))
        {
            _parent.Set(key, value);
            return;
        }

        // Default: create/update in current (global) scope
        _vars[key] = value;
    }

    /// Force-create a local variable in THIS scope only.
    public void DefineLocal(string name, object? value = null)
        => _vars[name.ToUpperInvariant()] = value;

    public bool Has(string name)
        => _vars.ContainsKey(name.ToUpperInvariant())
           || (_parent?.Has(name) ?? false);
}

public sealed class RuntimeException(string message) : Exception(message);