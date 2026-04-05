using System.Collections.Generic;
using UnityEngine;


public class TicketManager : MonoBehaviour 
{
    public List<TicketBase> activeTickets;
    [HideInInspector] public int maxRerolls;

    void Start() 
    {
        SetupGame();
    }

    void SetupGame() 
    {
        maxRerolls = 3; 

        foreach (var ticket in activeTickets)
        {
            ticket.Apply(this); 
        }
        
        Debug.Log($"최종 리롤 횟수: {maxRerolls}");
    }
}