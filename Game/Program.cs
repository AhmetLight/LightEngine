namespace Game;

public static class Program
{
    public static void Main()
    {
        using var game = new GameApp();
        game.Run();
    }
}
