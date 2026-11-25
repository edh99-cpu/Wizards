using System.Drawing.Drawing2D;

namespace Wizards;

internal sealed partial class TerritorySetup
{
    private readonly Button rollMove = new() { Text = "Roll movement", AutoSize = true, Enabled = false };
    private readonly Button endMove = new() { Text = "End movement", AutoSize = true, Enabled = false };
    private readonly Button nextTest = new() { Text = "Next test turn", AutoSize = true, Enabled = false };
    private readonly Button movementHelp = new() { Text = "Movement help", AutoSize = true };
    private enum MoveStage { Waiting, Moving, Finished }
    private MoveStage moveStage;
    private int whiteDie, redDie, remainingHalf, movedSteps;
    private int previewFortnight;
    private int AbsolutePreviewDay => previewFortnight * 14 + (initialState?.Day ?? 1);
    private int? occupiedBoat;
    private bool boarded, sailed;
    private string pendingEncounter = "", moveNote = "";
    private readonly Dictionary<Hex, Cell> cells = new();
    private readonly HashSet<(Hex,Hex)> walls = new();
    private enum Terrain { Plain, Woods, Mountain, Town, Abbey, Special, Crest, Stone, CircleCenter }
    private sealed record Cell(Placement Tile, string Code, Terrain Kind, string Encounter, int Stone = 0);

    // Artwork transcription: terrain is local to each tile and rotates with it.
    private static readonly Dictionary<string,string> Woods = new()
    {
        ["Common_2"]="A4 A5 B4 B5 C4 C5 Center",
        ["Common_3"]="C1 C2 C3 C4 B6",
        ["Common_4"]="C5 Center",
        ["Common_5"]="C4 C5 Center",
        ["Common_6"]="C2 B6 B3",
        ["Elven_1"]="A1 A3 A4 A5 A6 B3 B4 B5 B6 C2 C4 C6",
        ["Elven_2"]="B1 B4 B5 Center",
        ["Elven_3"]="B2 B4 B5 B6 C1 C2 C4 C5 C6",
        ["Elven_4"]="A3 A5 B4 B5 C1 C3 C4 C5",
        ["Elven_5"]="A2 A5 A6 B1 B3 B4 B6 C2 C3 C4 C5",
        ["Elven_6"]="A1 A2 A3 A4 A5 A6 B1 B2 B3 B4 B5 B6 C1 C2 C3 C4 C5 C6"
    };
    private static readonly Dictionary<string,string> Mountains = new()
    {
        ["Common_1"]="A3 A5 B4 C3 C5 C6",
        ["Common_3"]="A5 A6 B4",
        ["Common_4"]="A1 A2 A3 A6 B1 B2 B3 C6",
        ["Common_5"]="A5 A6",
        ["Common_6"]="A2 A4 A5 B1 B2 C3 C5 Center"
    };
    private static readonly string[] Inner = { "A4","A5","B4","B5","C4","C5" };
    private static readonly Dictionary<string,string> InteriorWalls = new()
    {
        ["Common_1"]="A1:A2 A2:A4 A3:A6 A4:A5 A4:C5 A5:A6 A5:B4 B1:B2 B2:B4 B4:B5 B3:B6 B5:B6 B5:C4 C1:C2 C2:C4 C3:C6 C4:C5 C5:C6",
        ["Common_2"]="A1:A4 A2:A4 A2:A5 A3:A5 A4:C6 B1:B4 B2:B4 B2:B5 B3:B5 B5:B6 B6:C4 C1:C4 C2:C5 C3:C5 C5:C6",
        ["Common_3"]="A1:A2 A1:A4 A4:C5 A4:C6 C5:Center C4:Center B5:C4 B6:C4 B6:C1",
        ["Magic_2"]="A1:A4 A2:A4 A2:A5 A3:A5 A4:C6 A5:A6 A6:B4 B1:B4 B2:B5 B3:B5 B4:B5 B5:B6 B6:C4 C1:C4 C2:C4 C2:C5 C3:C5 C5:C6 A4:Center A5:Center B4:Center C4:Center C5:Center",
        ["Magic_3"]="A1:A4 A2:A4 A2:A5 A3:A5 A4:C6 A5:A6 A6:B4 B2:B5 B3:B5 B4:B5 B5:B6 B6:C4 C1:C4 C2:C4 C2:C5 C3:C5 C5:C6 A4:Center A5:Center B4:Center B5:Center C4:Center",
        ["Magic_4"]="A1:A4 A2:A4 A2:A5 A3:A5 A4:C5 A4:C6 A5:A6 A6:B4 B1:B4 B2:B5 B3:B5 B4:B5 B5:B6 B6:C4 C1:C4 C2:C4 C2:C5 C3:C5 A5:Center B4:Center C4:Center C5:Center",
        ["Magic_5"]="A1:A4 A2:A4 A2:A5 A3:A5 A4:C6 B1:B4 B2:B4 B2:B5 B3:B5 B5:B6 B6:C4 C1:C4 C3:C5 C5:C6 A5:Center B4:Center C4:Center C5:Center",
        ["Magic_6"]="A1:A2 A1:A4 A2:A5 A3:A5 A4:A5 A4:Center A4:C6 A5:A6 A6:B4 B1:B4 B2:B4 B2:B5 B3:B5 B3:B6 B4:Center B5:Center B5:C4 B6:C4 C1:C4 C2:C4 C2:C5 C3:C5 C5:C6 C5:Center"
    };
    private static bool Listed(Dictionary<string,string> data,string key,string code) =>
        data.TryGetValue(key,out var list) && list.Split(' ').Contains(code);

    private void AddWall(Hex a,Hex b) { walls.Add((a,b)); walls.Add((b,a)); }
    private void BuildMovementMap()
    {
        cells.Clear(); walls.Clear();
        foreach(var p in placed)
        {
            string key=TileKey(p);
            var codes=new List<string>{"Center"};
            for(char a='A';a<='C';a++) for(int i=1;i<=6;i++) codes.Add(a.ToString()+i);
            foreach(string code in codes)
            {
                Terrain kind=Terrain.Plain; string encounter=""; int stone=0;
                if(Listed(Woods,key,code)) kind=Terrain.Woods;
                if(Listed(Mountains,key,code)) kind=Terrain.Mountain;
                if(key=="Magic_5") kind=Terrain.Woods;
                if(key=="Magic_6" || ((key is "Magic_2" or "Magic_3" or "Magic_4") && Inner.Contains(code))) kind=Terrain.Mountain;
                if(Settlements.TryGetValue(key,out var towns) && towns.Contains(code))
                {
                    kind=key=="Common_3" && code=="C6" ? Terrain.Abbey : Terrain.Town;
                    encounter=kind==Terrain.Abbey ? "Shrineheart Abbey" : key.StartsWith("Elven") ? "Elven dwelling" : "Common town";
                }
                if(key=="Elven_4" && code=="C2") { kind=Terrain.Crest; encounter="Star Crest"; }
                if(key=="Magic_1")
                {
                    if(code=="Center") { kind=Terrain.CircleCenter; encounter="Riiktal"; }
                    else if(Inner.Contains(code)) { kind=Terrain.Stone; stone=Array.IndexOf(Inner,code)+1; }
                }
                if(key.StartsWith("Magic_") && key!="Magic_1" && code=="Center")
                {
                    kind=Terrain.Special; encounter=key=="Magic_6" ? "Dragon" : key=="Magic_5" ? "Hamdrel" : "Master Sorcerer";
                }
                if(key=="Magic_3" && code=="B5") { kind=Terrain.Special; encounter="High Hollow"; }
                var h=Global(p,LocalSpace(code));
                cells.Add(h,new Cell(p,code,kind,encounter,stone));
            }
            if(InteriorWalls.TryGetValue(key,out var pairs))
                foreach(var pair in pairs.Split(' '))
                {
                    var names=pair.Split(':');
                    AddWall(Global(p,LocalSpace(names[0])),Global(p,LocalSpace(names[1])));
                }
            // White dashed exterior walls on Cottage and Lair. The A1/B3 gateways are open.
            if(key is "Magic_5" or "Magic_6")
            {
                var footprint=Footprint(p.Center).ToHashSet();
                foreach(string code in codes.Where(c=>c!="A1" && c!="B3"))
                {
                    var h=Global(p,LocalSpace(code));
                    foreach(var d in Clockwise)
                    {
                        var n=Add(h,d);
                        if(!footprint.Contains(n)) AddWall(h,n);
                    }
                }
            }
        }
    }

    private void AddMovementControls(FlowLayoutPanel tools)
    {
        rollMove.Click += (_,_)=>StartMovement();
        endMove.Click += (_,_)=>FinishMovement("");
        nextTest.Click += (_,_)=>AdvanceTest();
        movementHelp.Click += (_,_)=>MessageBox.Show(this,
            "Movement preview (rules 5–8): Roll white and red dice, then click adjacent hexes. Green outlines indicate legal steps. Plains cost 1, woods/mountains 2. Walls cannot be crossed. Swimming takes the full roll and moves only one water space. Click a boat to board (1 point); sailing costs half a point per water hex and cannot occur on the boarding turn. You cannot disembark on a sailing turn. End movement at any time. Click Resolve encounter after movement. Next test turn advances after resolution; unfinished card and Evil effects are not applied.",
            "Movement help");
        tools.Controls.Add(rollMove); tools.Controls.Add(endMove); tools.Controls.Add(nextTest); tools.Controls.Add(movementHelp);
        AddEncounterControls(tools);
    }
    private void RefreshMovement()
    {
        rollMove.Enabled=initialState!=null && moveStage==MoveStage.Waiting;
        endMove.Enabled=initialState!=null && moveStage==MoveStage.Moving;
        nextTest.Enabled=initialState!=null && moveStage==MoveStage.Finished && encounterResolved;
        nextTest.Text=initialState?.Day==13 && extraTurns==0 ? "Skip Evil Attack (test only)" : "Next test turn";
        ExpireEvents();
        RefreshEncounterControls();
    }
    private string MovementStatus()
    {
        var s=initialState!;
        string position=occupiedBoat.HasValue ? "Aboard boat" : cells.ContainsKey(s.Player) ? Describe(s.Player) : "Swimming";
        string dice=moveStage==MoveStage.Waiting ? "Roll movement" : $"White {whiteDie} | Red {redDie} | Remaining {remainingHalf/2.0:0.#}";
        return $"Fortnight {previewFortnight+1}, Day {s.Day} | {position} | {dice} | {(Afflicted?(dragonbound?"Dragonbound | ":"Demon Dazed | "):"")}{moveNote}";
    }
    private string Describe(Hex h)=>cells.TryGetValue(h,out var c)?TileLabel(c.Tile)+" "+c.Code+" ("+c.Kind+")":"Water";
    private void StartMovement()
    {
        if(initialState==null || moveStage!=MoveStage.Waiting)return;
        if(cells.Count==0)BuildMovementMap();
        whiteDie=Die(); redDie=Die(); remainingHalf=2*whiteDie; movedSteps=0; boarded=false; sailed=false;
        pendingEncounter=""; encounterResolved=false; ignoredTaskHex=null; moveStage=MoveStage.Moving;
        animalActive=animalTurns>0;
        Log($"Movement dice: white {whiteDie}, red {redDie}.");
        if(lostTurns>0 || fortnightTrap || (escapeTrap && whiteDie<5))
        {
            if(lostTurns>0)lostTurns--;
            pendingEncounter="No encounter";remainingHalf=0;moveStage=MoveStage.Finished;
            moveNote="Movement lost due to trap or lost turn; resolve end of turn.";UpdateStatus();canvas.Invalidate();return;
        }
        if(escapeTrap){escapeTrap=false;Log("Trap escaped; move immediately with freeing roll.");}
        if(forcedEncounter){forcedEncounter=false;pendingEncounter="Forced encounter";remainingHalf=0;moveStage=MoveStage.Finished;moveNote="Required repeat encounter; click Resolve encounter.";UpdateStatus();canvas.Invalidate();return;}
        ApplyMovementEventRoll();
        if(animal=="Horse" && !Afflicted)remainingHalf+=8;
        if(animal=="Hound" && !Afflicted && !occupiedBoat.HasValue)remainingHalf+=2;
        moveNote="Click an adjacent green hex, or End movement.";
        UpdateStatus();canvas.Invalidate();
    }
    private bool OnBoard(Hex h)
    {
        var p=Center(h);
        return p.X-2*Dx/3>=49 && p.X+2*Dx/3<=board.Width-49 && p.Y-Dy/2>=49 && p.Y+Dy/2<=board.Height-49;
    }
    private bool Adjacent(Hex a,Hex b)=>Clockwise.Any(d=>Add(a,d)==b);
    private bool PlanStep(Hex target,out int cost,out string action,out string error)
    {
        cost=0;action="";error="";
        if(initialState is not InitialState s || moveStage!=MoveStage.Moving) { error="Roll movement first.";return false; }
        if(!Adjacent(s.Player,target)) {error="Choose an adjacent hex.";return false;}
        if(!OnBoard(target)) {error="Outside the displayed board. Off-board sailing is not implemented.";return false;}
        if(walls.Contains((s.Player,target))) {error="A wall blocks this hexside.";return false;}
        bool fromLand=cells.ContainsKey(s.Player);
        bool toLand=cells.TryGetValue(target,out var c);
        int boat=s.Boats.IndexOf(target);
        if(occupiedBoat.HasValue)
        {
            if(toLand)
            {
                if(sailed){error="Cannot disembark on the turn the boat sails.";return false;}
                action="Disembark";
            }
            else if(boat>=0 && !sailed)
            {
                cost=2;action="Board";return Affordable(cost,out error);
            }
            else
            {
                if(boarded){error="Cannot sail on the turn you board a boat.";return false;}
                cost=dragonbound?4:demonDazed||demons.Contains(s.Player)?2:1;action="Sail";return Affordable(cost,out error);
            }
        }
        else if(!toLand)
        {
            if(boat>=0){cost=2;action="Board";return Affordable(cost,out error);}
            if(!fromLand){error="Cannot swim from one water hex into another.";return false;}
            if(movedSteps!=0){error="Swimming requires the full movement roll.";return false;}
            cost=remainingHalf;action="Swim";return true;
        }
        else action="Walk";

        if(dragonbound && c!.Encounter=="High Hollow"){error="Dragonbound players cannot enter High Hollow.";return false;}
        if(c!.Kind==Terrain.Crest && (dragonbound || (!crestReentry&&!TaskAuthorizesCrest(target)&&!HasCrestPass()))){error="Star Crest requires authorization or permitted re-entry.";return false;}
        if(c.Kind==Terrain.Stone)
        {
            if(movedSteps!=0 || whiteDie!=c.Stone){error=$"Entering this stone requires an adjacent starting position and white die {c.Stone}.";return false;}
            cost=remainingHalf;action="Enter stone";return true;
        }
        if(c.Kind==Terrain.CircleCenter)
        {
            if(movedSteps!=0 || !cells.TryGetValue(s.Player,out var start) || start.Kind!=Terrain.Stone || whiteDie>3)
            {error="Riiktal is accessible from a stone with white die 1–3.";return false;}
            cost=remainingHalf;action="Enter circle";return true;
        }
        cost=EventLandCost(c.Kind,(Afflicted || (c.Kind is Terrain.Woods or Terrain.Mountain))?4:2);
        return Affordable(cost,out error);
    }
    private bool Affordable(int cost,out string error)
    {
        error=cost>remainingHalf?"Not enough movement points.":"";
        return cost<=remainingHalf;
    }
    private void MoveClick(Hex target)
    {
        if(initialState is not InitialState s)return;
        if(!PlanStep(target,out int cost,out string action,out string reason))
        {moveNote=reason;UpdateStatus();return;}
        if(action=="Board"){occupiedBoat=s.Boats.IndexOf(target);boarded=true;}
        else if(action=="Sail"){s.Boats[occupiedBoat!.Value]=target;sailed=true;}
        else if(action=="Disembark")occupiedBoat=null;
        else if(cells.ContainsKey(target))occupiedBoat=null;
        var previous=s.Player;
        if((animal is "Horse" or "Hound") && ((action=="Board" && animal=="Horse") || (cells.TryGetValue(target,out var animalCell) && ((TileKey(animalCell.Tile) is "Magic_1" or "Magic_6") || animalCell.Kind==Terrain.Crest)))){animal="";animalTurns=0;}
        if(cells.TryGetValue(previous,out var previousCell) && previousCell.Kind==Terrain.Crest && !usedCrestReentry){crestReentry=true;crestDepartureDay=AbsolutePreviewDay;}
        if(cells.TryGetValue(target,out var newCell) && newCell.Kind==Terrain.Crest)
        {
            if(crestReentry){crestReentry=false;usedCrestReentry=true;}
            else if(!TaskAuthorizesCrest(target))UseCrestPass();
        }
        UpdateLair(previous,target);
        s.Player=target;s.PlayerLocation=Describe(target);remainingHalf-=cost;movedSteps++;
        moveNote=action+" → "+Describe(target);
        // A Dragon away from its lair returns when the player enters the lair (8.421).
        if(cells.TryGetValue(target,out var cell) && TileKey(cell.Tile)=="Magic_6" &&
            (!cells.TryGetValue(s.Dragon,out var dragonCell) || TileKey(dragonCell.Tile)!="Magic_6"))
            (s.Dragon,s.DragonLocation)=RandomSpace(cell.Tile);
        ignoredTaskHex=null;
        OfferTaskPickup(target);
        string specific=SpecificAt(target);
        if(specific.Length>0)FinishMovement(specific);
        else if(remainingHalf==0)FinishMovement("");
        else {UpdateStatus();canvas.Invalidate();}
    }
    private string SpecificAt(Hex h)
    {
        var list=new List<string>();
        if(TaskAt(h))list.Add("Task Marker");
        if(revealedWizards.ContainsKey(h))list.Add("High Wizard");
        if(cells.TryGetValue(h,out var c) && c.Encounter.Length>0)list.Add(c.Encounter);
        if(demons.Contains(h))list.Add("Demons");
        if(initialState!.Dragon==h && !list.Contains("Dragon"))list.Add("Dragon");
        return string.Join(" then ",list);
    }
    private bool RandomEligible(Hex h)
    {
        if(dragonbound || Trapped || TaskAt(h) || demons.Contains(h) || revealedWizards.ContainsKey(h) || !cells.TryGetValue(h,out var c))return false;
        string key=TileKey(c.Tile);
        return c.Kind is not (Terrain.Stone or Terrain.CircleCenter)
            && key is not ("Magic_5" or "Magic_6")
            && !((key is "Magic_2" or "Magic_3" or "Magic_4") && c.Kind==Terrain.Mountain)
            && c.Encounter.Length==0 && initialState!.Dragon!=h;
    }
    private void FinishMovement(string specific)
    {
        if(initialState==null || moveStage!=MoveStage.Moving)return;
        // Merely remaining in the starting space does not repeat a specific encounter.
        if(specific.Length==0 && movedSteps>0)specific=SpecificAt(initialState.Player);
        pendingEncounter=specific.Length>0?"Specific encounter: "+specific :
            redDie<=3 && RandomEligible(initialState.Player)?"Random encounter":"No encounter";
        remainingHalf=0;moveStage=MoveStage.Finished;
        moveNote=pendingEncounter+". Click Resolve encounter.";
        UpdateStatus();canvas.Invalidate();
    }
    private void AdvanceTest()
    {
        if(initialState==null || moveStage!=MoveStage.Finished || !encounterResolved)return;
        bool skipAttack=initialState.Day==13 && extraTurns==0;
        if(skipAttack && MessageBox.Show(this,"Day 14 Evil Attack is not implemented. Skip its resolution and begin the next fortnight for testing only? No Evil Attack effects will be applied.", "Test-only calendar bypass",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
        if(!skipAttack && deferredEffects && MessageBox.Show(this,"Some card or special effects are pending. Continue for testing only?", "Encounter preview",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
        if(crestReentry && AbsolutePreviewDay>crestDepartureDay)crestReentry=false;
        if(animalActive && animalTurns>0){animalTurns--;if(animalTurns==0)animal="";}
        animalActive=false;
        if(extraTurns>0){extraTurns--;moveNote="Extra turn — roll movement.";}
        else if(skipAttack)
        {
            initialState.Day=14;
            Log("TEST ONLY — Evil Attack resolution skipped.");
            if(animalTurns>0){animalTurns--;if(animalTurns==0)animal="";}
            if(lostTurns>0)lostTurns--;
            if(lairDeadline==14&&!dragonbound){dragonbound=true;lairDeadline=null;animal="";animalTurns=0;DiscardAllEvents("Dragonbound");Log("Dragonbound after remaining in the lair.");}
            fortnightTrap=false;
            previewFortnight++;
            initialState.Day=1;
            deferredEffects=true;
            moveNote="Test fortnight — Evil Attack skipped. Roll movement.";
        }
        else{initialState.Day++;moveNote="Test day — roll movement.";}
        if(lairDeadline==initialState.Day && !dragonbound){dragonbound=true;lairDeadline=null;animal="";animalTurns=0;DiscardAllEvents("Dragonbound");Log("Dragonbound after remaining in the lair.");}
        moveStage=MoveStage.Waiting;encounterResolved=false;pendingEncounter="";UpdateStatus();canvas.Invalidate();
    }
    private void DrawMovementHints(Graphics g)
    {
        if(initialState is not InitialState s || moveStage!=MoveStage.Moving)return;
        using var pen=new Pen(Color.LimeGreen,5);
        using var fill=new SolidBrush(Color.FromArgb(40,Color.LimeGreen));
        foreach(var d in Clockwise)
        {
            Hex h=Add(s.Player,d);
            if(!PlanStep(h,out _,out _,out _))continue;
            var poly=HexPolygon(h);g.FillPolygon(fill,poly);g.DrawPolygon(pen,poly);
        }
    }
}
