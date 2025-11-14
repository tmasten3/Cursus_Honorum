/// <summary>
/// Compatibility shim preserving the legacy Assembly-CSharp::GameController identifier
/// for existing scenes and prefabs. Forwarding to the namespaced implementation ensures
/// scene bindings remain valid while centralizing logic in <see cref="Game.Core.GameController"/>.
/// </summary>
public class GameController : Game.Core.GameController
{
}
