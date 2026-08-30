namespace ThronefallControl.Game;

public sealed class GameFacade
{
    public static GameFacade? Current { get; set; }

    public IdRegistry Ids { get; } = new();

    public Units Units { get; }

    public GameFacade(IUnitWorld? world = null)
    {
        Units = new Units(world ?? new LiveUnitWorld(this));
    }

    public static GameFacade CreateLive()
    {
        var facade = new GameFacade();
        Current = facade;
        Units.Current = facade.Units;
        return facade;
    }
}
