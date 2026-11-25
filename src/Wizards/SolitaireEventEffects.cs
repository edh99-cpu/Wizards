namespace Wizards;
internal sealed partial class TerritorySetup
{
    // End is exclusive; "next N days" starts next calendar day.
    private readonly Dictionary<string,(int Start,int End)> timedEvents=new();
    private static bool MovementCard(int n)=>n>=33&&n<=43;
    private static bool IsAutomatedEvent(int n)=>(n>=1&&n<=8)||(n>=10&&n<=17)||(n>=19&&n<=23)||n is 25 or 27 or 28 or 29 or 30 or 31 or 32 or 34 or 35 or 36 or 37 or 38 or 39 or 40 or 41 or 42 or 43 or 45 or 46 or 47 or 49;
    private void HoldEvent(string name)
    {
        if(!events.Contains(name))events.Add(name);
        Log(name+" held for later use.");
    }
    private void HoldTimedEvent(string name,int days,bool beginsNow)
    {
        HoldEvent(name);
        int start=AbsolutePreviewDay+(beginsNow?0:1);
        timedEvents[name]=(start,start+days);
        Log(name+" active from "+DayText(start)+" through "+DayText(start+days-1)+".");
    }
    private static string DayText(int day)=>$"Fortnight {(day-1)/14+1}, Day {(day-1)%14+1}";
    private void DiscardEvent(string name,string reason)
    {
        if(events.Remove(name)&&!eventDiscard.Contains(name))eventDiscard.Add(name);
        timedEvents.Remove(name);
        Log(name+" discarded: "+reason+".");
    }
    private void DiscardAllEvents(string reason)
    {
        foreach(string name in events.ToArray())DiscardEvent(name,reason);
    }
    private void ExpireEvents()
    {
        if(initialState==null)return;
        foreach(var entry in timedEvents.ToArray())
            if(AbsolutePreviewDay>=entry.Value.End)DiscardEvent(entry.Key,"duration ended");
        if(Afflicted||Trapped)CancelMovementEvent("afflicted or trapped");
    }
    private void CancelMovementEvent(string reason)
    {
        foreach(string name in events.Where(x=>MovementCard(CardNumber(x))).ToArray())DiscardEvent(name,reason);
    }
    private void HoldMovementEvent(string name,int days)
    {
        CancelMovementEvent("replaced by another Movement Event");
        if(Afflicted||Trapped){eventDiscard.Add(name);Log(name+" discarded because player is afflicted or trapped.");return;}
        HoldTimedEvent(name,days,false);
    }
    private bool EventActive(string name)=>events.Contains(name)&&(!timedEvents.TryGetValue(name,out var t)||(AbsolutePreviewDay>=t.Start&&AbsolutePreviewDay<t.End));
    private string? ActiveCard(params int[] nums)=>events.FirstOrDefault(x=>nums.Contains(CardNumber(x))&&EventActive(x));
    private int ActiveMovement()=>events.Where(x=>MovementCard(CardNumber(x))&&EventActive(x)).Select(CardNumber).FirstOrDefault();
    private void ApplyMovementEventRoll()
    {
        int n=ActiveMovement();
        if(n==39)remainingHalf+=4;
        else if(n is 40 or 42)remainingHalf*=2;
        else return;
        Log($"Event {n}: adjusted movement allowance to {remainingHalf/2.0:0.#}.");
    }
    private int EventLandCost(Terrain kind,int normal)
    {
        if(Afflicted)return normal;
        int n=ActiveMovement();
        if(n==34)return 1;
        if(n==43&&(kind is Terrain.Plain or Terrain.Woods or Terrain.Mountain))return 1;
        if(kind==Terrain.Woods&&(n is 35 or 36 or 41))return 2;
        if(kind==Terrain.Mountain&&(n is 36 or 38 or 41))return 2;
        return normal;
    }
    private bool HasCrestPass()=>ActiveCard(23,27)!=null;
    private void UseCrestPass()
    {
        string? name=ActiveCard(23,27);
        if(name!=null)DiscardEvent(name,"one permitted Star Crest entry used");
    }
    private bool ProtectedFromTraps()=>ActiveCard(25,28)!=null;
    private bool ProtectTheft()
    {
        if(ActiveCard(25,28)!=null){Log("Theft prevented by active protection Event.");return true;}
        string? name=ActiveCard(29);
        if(name!=null&&MessageBox.Show(this,"Use Event 29 to protect all Magical Objects and Gems from theft?","Event 29",MessageBoxButtons.YesNo)==DialogResult.Yes)
        {DiscardEvent(name,"one theft prevented");return true;}
        return false;
    }
    private bool TaskSuspended()=>ActiveCard(31)!=null;
    private void AnswerSummons(Hex h)
    {
        if(ActiveCard(31) is string card && cells.TryGetValue(h,out var c) && (c.Encounter=="Riiktal"||(TileKey(c.Tile)=="Common_1"&&c.Code=="Center")||(TileKey(c.Tile)=="Elven_1"&&c.Code=="C3")))
        {DiscardEvent(card,"summons answered");Log("Tasks may be accepted and fulfilled again.");OfferTaskPickup(h);}
    }
    private readonly Button eventTravel=new(){Text="Event travel",AutoSize=true,Enabled=false};
    private void UseEventTravel()
    {
        if(initialState==null||moveStage!=MoveStage.Waiting||Afflicted||Trapped||lostTurns>0||forcedEncounter)return;
        string? pass=ActiveCard(30), travel=ActiveCard(37);Hex? target=null;
        if(pass!=null && (travel==null||MessageBox.Show(this,"Use single-use Event 30 for Star Crest instead of Event 37?","Event travel",MessageBoxButtons.YesNo)==DialogResult.Yes))
        {
            target=FindEncounter("Star Crest");if(!target.HasValue){Log("Star Crest is not on the board.");return;}DiscardEvent(pass,"Star Crest transportation used");
        }
        else if(travel!=null)
        {
            target=PickHex(cells.Where(x=>x.Value.Kind!=Terrain.CircleCenter).Select(x=>x.Key).Concat(WaterHexes()),"Event 37 — choose destination");
        }
        if(!target.HasValue)return;
        var s=initialState;var before=s.Player;animalActive=animalTurns>0;whiteDie=0;redDie=0;remainingHalf=0;movedSteps=1;moveStage=MoveStage.Finished;encounterResolved=false;ignoredTaskHex=null;
        s.Player=target.Value;s.PlayerLocation=ObjectiveLabel(target.Value);occupiedBoat=null;UpdateLair(before,target.Value);OfferTaskPickup(target.Value);
        string specific=SpecificAt(target.Value);pendingEncounter=specific.Length>0?"Specific encounter: "+specific:"No encounter";
        moveNote=pendingEncounter+" — Event travel. Click Resolve encounter.";Log("Event travel to "+s.PlayerLocation+".");UpdateStatus();canvas.Invalidate();
    }
    private string ActiveEventRecord()
    {
        var lines=events.Select(name=>name+(timedEvents.TryGetValue(name,out var t)?" — "+(AbsolutePreviewDay<t.Start?"begins "+DayText(t.Start):"active")+", expires after "+DayText(t.End-1):" — held/pending"));
        return events.Count==0?"":string.Join("\r\n",lines)+"\r\n";
    }
}
