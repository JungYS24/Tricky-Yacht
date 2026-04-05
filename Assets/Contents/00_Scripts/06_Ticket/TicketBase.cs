using UnityEngine;

public abstract class TicketBase : ScriptableObject 
{
    public string Name;
    [TextArea] public string Description;
    public int Level;

    public abstract void Apply(TicketManager manager);
}