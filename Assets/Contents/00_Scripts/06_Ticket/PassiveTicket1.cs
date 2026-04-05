using UnityEngine;


[CreateAssetMenu(menuName = "Tricky/Tickets/PassiveTicket1")]
public class PassiveTicket1 : TicketBase 
{
    public int RerollAddPerLevel = 1;

   
    public override void Apply(TicketManager manager) 
    {
        manager.maxRerolls += Level * RerollAddPerLevel;
    }
}