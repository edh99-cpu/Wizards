namespace Wizards;

internal sealed partial class TerritorySetup
{
    private readonly Button resolveEncounter = new() { Text = "Resolve encounter", AutoSize = true, Enabled = false };
    private readonly Button encounterHistory = new() { Text = "Encounter log", AutoSize = true, Enabled = false };
    private readonly Button inspectCards = new() { Text = "Drawn cards", AutoSize = true, Enabled = false };
    private readonly List<string> encounterLog = new();
    private readonly List<(string Name,bool Mandatory)> tasks = new();
    private readonly List<string> events = new();
    private readonly HashSet<Hex> demons = new();
    private readonly Dictionary<Hex,List<string>> revealedWizards = new();
    private bool encounterResolved, deferredEffects;
    private bool demonDazed, dragonbound, escapeTrap, fortnightTrap, forcedEncounter;
    private string repeatEncounter = "";
    private int? testRoll;
    private readonly Button testEncounter = new() { Text = "Test encounter…", AutoSize = true, Enabled = false };
    private int lostTurns, extraTurns, lastPromotionDay = -100;
    private string animal = "";
    private int animalTurns;
    private bool animalActive;
    private int? lairDeadline;
    private int encounterDepth;
    private readonly Dictionary<Hex,int> locationVisits = new();
    private string? lastTerritory;
    private bool crestReentry;
    private int crestDepartureDay;
    private bool usedCrestReentry;
    private static readonly string[] WizardNames = { "Ishkatar","Aevarex","Elekov","Terek","Tolmitar","Veldor","Zegoral" };

    private void AddEncounterControls(FlowLayoutPanel tools)
    {
        resolveEncounter.Click += (_,_) => ResolvePending();
        encounterHistory.Click += (_,_) => ShowEncounterLog();
        inspectCards.Click += (_,_) => ShowDrawnCards();
        testEncounter.Click += (_,_) => ChooseTestEncounter();
        taskProgress.Click += (_,_) => ShowTaskProgress();
        tools.Controls.Add(resolveEncounter); tools.Controls.Add(encounterHistory); tools.Controls.Add(inspectCards); tools.Controls.Add(taskProgress); tools.Controls.Add(testEncounter);
    }
    private void RefreshEncounterControls()
    {
        resolveEncounter.Enabled = initialState != null && moveStage == MoveStage.Finished && !encounterResolved;
        testEncounter.Enabled = initialState != null && moveStage == MoveStage.Finished && !encounterResolved;
        encounterHistory.Enabled = encounterLog.Count > 0;
        inspectCards.Enabled = tasks.Count + events.Count > 0;
        taskProgress.Enabled = initialState != null;
    }
    private void Log(string message) => encounterLog.Add($"Fortnight {previewFortnight+1}, Day {initialState!.Day}: {message}");
    private int HighRoll(string label, int? forced = null)
    {
        int a=Die(), b=Die(), result=Math.Max(a,b);
        if(testRoll.HasValue){result=testRoll.Value;testRoll=null;Log($"TEST {label}: selected result {result}.");return result;}
        if(forced.HasValue) result=forced.Value;
        Log($"{label}: rolled {a}, {b}; higher die = {result}.");
        return result;
    }
    private int SingleRoll(string label) { int n=Die(); Log($"{label}: rolled {n}."); return n; }
    private void ShowEncounterLog()
    {
        using var f = new Form { Text="Wizards — Encounter log", StartPosition=FormStartPosition.CenterParent,
            ClientSize=new Size(720,490), MinimizeBox=false };
        var box=new TextBox { Dock=DockStyle.Fill, Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Vertical,
            Font=new Font("Segoe UI",11), Text=string.Join(Environment.NewLine,encounterLog) };
        f.Controls.Add(box); f.Shown+=(_,_)=>{box.SelectionStart=box.TextLength;box.ScrollToCaret();}; f.ShowDialog(this);
    }
    private void ShowCard(string name,string title)
    {
        using var image=LoadImage(Path.Combine(artworkFolder,name+".png"));
        using var f=new Form { Text=title,StartPosition=FormStartPosition.CenterParent, ClientSize=new Size(570,700), MinimizeBox=false };
        var picture=new PictureBox { Image=image, Dock=DockStyle.Fill, SizeMode=PictureBoxSizeMode.Zoom };
        var close=new Button { Text="Close card", Dock=DockStyle.Bottom,Height=38,DialogResult=DialogResult.OK };
        f.Controls.Add(picture);f.Controls.Add(close);f.AcceptButton=close;f.ShowDialog(this);
    }
    private void ShowDrawnCards()
    {
        using var f=new Form {Text="Wizards — Active cards",StartPosition=FormStartPosition.CenterParent,ClientSize=new Size(460,380)};
        var list=new ListBox {Dock=DockStyle.Fill};
        var cards=tasks.Select(x=>(x.Name,Label:(x.Mandatory?"Mandatory ":"Optional ")+x.Name+(activeTasks.ContainsKey(x.Name)?" (active)":" (pending)"))).Concat(events.Select(x=>(Name:x,Label:x))).ToList();
        foreach(var c in cards) list.Items.Add(c.Label);
        var open=new Button { Text="View selected card",Dock=DockStyle.Bottom,Height=40 };
        open.Click+=(_,_)=>{if(list.SelectedIndex>=0)ShowCard(cards[list.SelectedIndex].Name,cards[list.SelectedIndex].Label);};
        list.DoubleClick+=(_,_)=>{if(list.SelectedIndex>=0)ShowCard(cards[list.SelectedIndex].Name,cards[list.SelectedIndex].Label);};
        f.Controls.Add(list);f.Controls.Add(open);f.ShowDialog(this);
    }
    private void DrawTask(bool mandatory,string? source=null)
    {
        var s=initialState!;
        if(s.Rank==0 || s.Rank==4 || dragonbound){Log("Task not assigned at this rank or while Dragonbound.");return;}
        while(s.TaskDeck.Count>0)
        {
            string name=s.TaskDeck[0];s.TaskDeck.RemoveAt(0);
            bool known=TaskDefinitions.TryGetValue(CardNumber(name),out var definition);
            if(known && !TaskPossible(definition!))
            {
                taskDiscard.Add(name);
                Log(TaskLabel(name)+" needs a missing Territory; set aside and redraw (solitaire preview interpretation).");
                continue;
            }
            tasks.Add((name,mandatory));
            Log($"{(mandatory?"Mandatory":"Optional")} task drawn: {name}.");
            ShowCard(name,"Task — "+(mandatory?"Mandatory":"Optional")+(known?"":" (automation pending)"));
            if(known)RegisterTask(name,definition!,source);
            else {deferredEffects=true;Log("Card-specific task automation pending.");}
            return;
        }
        Log("Task deck exhausted; discard recycling not implemented.");deferredEffects=true;
    }
    private void DrawEvent()
    {
        var s=initialState!;
        if(s.EventDeck.Count==0){Log("Event deck exhausted; discard recycling not implemented.");deferredEffects=true;return;}
        string name=s.EventDeck[0];s.EventDeck.RemoveAt(0);
        Log($"Event drawn: {name}.");ShowCard(name,"Event");
        if(ApplyEventCard(name)){eventDiscard.Add(name);Log("Immediate Event resolved and discarded: "+name+".");}
        else {events.Add(name);deferredEffects=true;Log("Event effect pending or required destination unavailable: "+name+".");}
    }
    private void Award(int amount)
    {
        var s=initialState!;
        if(s.Rank is 0 or 4 || dragonbound){Log("No points gained at this rank or while Dragonbound.");return;}
        using var f=new Form {Text=$"Allocate {amount} points",StartPosition=FormStartPosition.CenterParent,ClientSize=new Size(365,195),FormBorderStyle=FormBorderStyle.FixedDialog,ControlBox=false};
        var k=new NumericUpDown {Minimum=0,Maximum=amount,Left=180,Top=18,Width=140};
        var p=new NumericUpDown {Minimum=0,Maximum=amount,Left=180,Top=52,Width=140};
        var w=new NumericUpDown {Minimum=0,Maximum=amount,Left=180,Top=86,Width=140};
        f.Controls.Add(new Label {Text="Knowledge",Left=20,Top=20,AutoSize=true});
        f.Controls.Add(new Label {Text="Perception",Left=20,Top=54,AutoSize=true});
        f.Controls.Add(new Label {Text="Power",Left=20,Top=88,AutoSize=true});
        k.Value=amount;f.Controls.Add(k);f.Controls.Add(p);f.Controls.Add(w);
        var ok=new Button {Text="Allocate points",Left=20,Top=132,Width=300,Height=36};
        void Check()=>ok.Enabled=k.Value+p.Value+w.Value==amount;
        k.ValueChanged+=(_,_)=>Check();p.ValueChanged+=(_,_)=>Check();w.ValueChanged+=(_,_)=>Check();
        ok.Click+=(_,_)=>f.DialogResult=DialogResult.OK;f.Controls.Add(ok);f.AcceptButton=ok;
        if(f.ShowDialog(this)==DialogResult.OK){s.Knowledge+=(int)k.Value;s.Perception+=(int)p.Value;s.Power+=(int)w.Value;Log($"Gained K {k.Value}, Per {p.Value}, Pow {w.Value}.");}
    }
    private void LosePoints(int amount,bool least)
    {
        var s=initialState!;
        if(s.Rank is 0 or 4){Log("No points lost at this rank.");return;}
        var points=new[]{s.Knowledge,s.Perception,s.Power};
        var names=new[]{"Knowledge","Perception","Power"};
        var positive=Enumerable.Range(0,3).Where(i=>points[i]>0).ToArray();
        if(positive.Length==0){Log("No points to lose.");return;}
        var options=least?positive.Where(i=>points[i]==positive.Min(j=>points[j])).ToArray():positive;
        int index=options[0];
        if(options.Length>1)
        {
            using var f=new Form {Text=$"Lose {amount} points",StartPosition=FormStartPosition.CenterParent,ClientSize=new Size(330,110),ControlBox=false};
            var list=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Left=15,Top=15,Width=300};
            foreach(int i in options)list.Items.Add(names[i]);list.SelectedIndex=0;
            var ok=new Button {Text="Confirm loss",Left=15,Top=55,Width=300,DialogResult=DialogResult.OK};f.Controls.Add(list);f.Controls.Add(ok);f.AcceptButton=ok;f.ShowDialog(this);
            index=options[list.SelectedIndex];
        }
        int actual=Math.Min(points[index],amount);points[index]-=actual;
        (s.Knowledge,s.Perception,s.Power)=(points[0],points[1],points[2]);Log($"Lost {actual} {names[index]} points.");
    }
    private void Theft(){Log("Magical Objects and Gems stolen.");FailStolenTasks();if(tasks.Any(t=>!activeTasks.ContainsKey(t.Name))){deferredEffects=true;Log("Check unresolved Tasks for additional stolen objects.");}}
    private void Heal(){demonDazed=false;dragonbound=false;Log("Afflictions healed. No other benefit from this encounter.");}
    private bool Afflicted=>demonDazed||dragonbound;
    private bool Trapped=>escapeTrap||fortnightTrap;
    private Hex RandomLand(Placement? tile=null)
    {
        Placement p=tile??placed[Random.Shared.Next(placed.Count)];
        return RandomSpace(p).Hex;
    }
    private Hex LairCenter()=>placed.Single(p=>TileKey(p)=="Magic_6").Center;
    private Hex LairRandom()=>RandomLand(placed.Single(p=>TileKey(p)=="Magic_6"));
    private Hex? FindEncounter(string name)=>cells.Where(x=>x.Value.Encounter==name).Select(x=>(Hex?)x.Key).FirstOrDefault();
    private void UpdateLair(Hex before,Hex after)
    {
        bool InLair(Hex h)=>cells.TryGetValue(h,out var c)&&TileKey(c.Tile)=="Magic_6";
        if(!InLair(after)){lairDeadline=null;return;}
        if(!InLair(before)&&!dragonbound)lairDeadline=(initialState!.Day+12)%14+1;
    }
    private void Transport(Hex destination)
    {
        var s=initialState!;var before=s.Player;
        crestReentry=false;usedCrestReentry=false;
        s.Player=destination; s.PlayerLocation=Describe(destination); occupiedBoat=null;
        escapeTrap=false;fortnightTrap=false;lostTurns=0;forcedEncounter=false;animal="";animalTurns=0;animalActive=false;
        UpdateLair(before,destination);
        Log("Transported to "+Describe(destination)+".");
        if(encounterDepth>=12){Log("Encounter chain paused at safety limit; further resolution pending.");deferredEffects=true;return;}
        ResolveSpecific(destination,true);
    }
    private void TransportNamed(string encounter)
    {
        Hex? h=FindEncounter(encounter);
        if(h.HasValue)Transport(h.Value);
        else {Log("Named destination unavailable. Removed-territory handling requires further rule implementation.");deferredEffects=true;}
    }
    private void Mentor(string order,string name,bool taskFulfilled=false)
    {
        var s=initialState!;
        if(Afflicted){Heal();return;}
        string? source=order=="Wizards"&&WizardNames.Contains(name)?"wizard:"+name:order!="Wizards"?"mentor":null;
        if(s.Order==order && source=="mentor")taskFulfilled=ResolveTaskMeeting(source)||taskFulfilled;
        if(s.Order!=order && taskFulfilled)return;
        if(s.Order!=order)
        {
            if(MessageBox.Show(this,$"{name} offers membership in the Order of {order}. Accept?", "Magical Order",MessageBoxButtons.YesNo)==DialogResult.Yes)
            {
                if(s.Order!="None")
                    for(int i=0;i<tasks.Count;i++)tasks[i]=(tasks[i].Name,false);
                s.Order=order;s.Rank=1;lastPromotionDay=AbsolutePreviewDay;Log($"Joined Order of {order} as Apprentice (Rank 1).");DrawTask(true,source);
                return;
            }
            if(s.Rank>0&&s.Rank<4)DrawTask(false,source);
            return;
        }
        int[][] thresholds=order=="Wizards"?new[]{new[]{15,10,5},new[]{40,30,20},new[]{75,60,45}}:
            order=="Sorcerers"?new[]{new[]{10,5,15},new[]{30,20,40},new[]{60,45,75}}:
            new[]{new[]{5,15,10},new[]{20,40,30},new[]{45,75,60}};
        if(s.Rank>=1&&s.Rank<4)
        {
            var t=thresholds[s.Rank-1];
            if(AbsolutePreviewDay-lastPromotionDay>=3&&s.Knowledge>=t[0]&&s.Perception>=t[1]&&s.Power>=t[2])
            {s.Rank++;lastPromotionDay=AbsolutePreviewDay;Log($"Promoted to Rank {s.Rank}.");}
            if(s.Rank<4)DrawTask(true,source);
        }
        else if(s.Rank==4){Log("Rank 4 mentor services and Gem collection require completed task and spell systems.");deferredEffects=true;}
    }
    private List<string> TakeWizardCards(bool onlyHigh)
    {
        var s=initialState!;var drawn=new List<string>();
        while(s.WizardDeck.Count>0)
        {
            string card=s.WizardDeck[0];s.WizardDeck.RemoveAt(0);drawn.Add(card);
            if(!onlyHigh||WizardNames.Any(n=>card=="Wizard_Card_"+n))break;
        }
        return drawn;
    }
    private void ReturnWizardCards(IEnumerable<string> drawn)
    {initialState!.WizardDeck.AddRange(drawn);Shuffle(initialState.WizardDeck);}
    private void MeetHigh(string card)
    {
        string name=card.Replace("Wizard_Card_","");
        Log("High Wizard encountered: "+name+".");
        bool taskFulfilled=!Afflicted&&ResolveTaskMeeting("wizard:"+name);
        Mentor("Wizards",name,taskFulfilled);
        if(cells.TryGetValue(initialState!.Player,out var c)&&TileKey(c.Tile)=="Magic_6"&&!dragonbound)
            lairDeadline=(initialState.Day+12)%14+1; // 2003 correction 4
    }
    private void WizardDraw(bool onlyHigh,bool revealLocation=false)
    {
        var drawn=TakeWizardCards(onlyHigh);
        if(drawn.Count==0){Log("No Wizard cards available.");return;}
        string card=drawn[^1];
        bool high=WizardNames.Any(n=>card=="Wizard_Card_"+n);
        Log("Wizard card: "+card.Replace("Wizard_Card_","").Replace('_',' '));
        if(onlyHigh&&!high){ReturnWizardCards(drawn);Log("No High Wizard available in the draw deck.");return;}
        if(high&&revealLocation)
        {
            Hex h=RandomLand();
            if(!revealedWizards.TryGetValue(h,out var stack))revealedWizards[h]=stack=new();
            stack.AddRange(drawn);
            Log("High Wizard location revealed: "+Describe(h)+".");
            if(h==initialState!.Player)ResolveRevealed(h);
            return;
        }
        ReturnWizardCards(drawn);
        if(high){MeetHigh(card);return;}
        if(card.Contains("Phantasm")){Log("Phantasm: no encounter.");return;}
        if(card.Contains("Star_Crest"))TransportNamed("Star Crest");
        else if(card.Contains("Hamdrel"))TransportNamed("Hamdrel");
        else if(card.Contains("Shrineheart"))TransportNamed("Shrineheart Abbey");
        else if(card.Contains("Dragon"))Transport(LairCenter());
        else {Log("Unresolved Wizard card.");deferredEffects=true;}
    }
    private void ResolveRevealed(Hex h)
    {
        if(!revealedWizards.Remove(h,out var cards))return;
        var highs=cards.Where(c=>WizardNames.Any(n=>c=="Wizard_Card_"+n)).ToList();
        ReturnWizardCards(cards);
        foreach(string card in highs){MeetHigh(card);if(initialState!.Player!=h)break;}
    }
    private void CommonFolk()
    {
        switch(HighRoll("Common Folk (§7.301)"))
        {case 1:Theft();break;case 2:LosePoints(SingleRoll("Point loss"),false);break;
        case 3:forcedEncounter=true;repeatEncounter="Common Folk";Log("Detained: encounter Common Folk again next turn.");break;
        case 4:DrawTask(false);break;case 5:extraTurns++;Log("One immediate extra turn.");break;case 6:Log("Lodging; no effect.");break;}
    }
    private void DemonEncounter()
    {
        var s=initialState!;var h=s.Player;demons.Add(h);
        if(animal=="Horse"||animal=="Hound"){animal="";animalTurns=0;}
        int result=HighRoll("Demons (§7.304)");
        if(result==2&&s.Rank==4)result=3;
        if(result!=6)
        {
            Hex extra=cells.TryGetValue(h,out var c)?RandomLand(c.Tile):RandomLand();
            demons.Add(extra);Log("Demon remains; additional Demon placed at "+Describe(extra)+".");
        }
        switch(result)
        {case 1:lostTurns+=SingleRoll("Turns lost");break;
        case 2:LosePoints(SingleRoll("Point loss"),true);break;
        case 3:Theft();break;
        case 4:demonDazed=true;animal="";animalTurns=0;Log("Demon Dazed.");if(events.Count>0)deferredEffects=true;break;
        case 5:Transport(LairRandom());break;
        case 6:demons.Remove(h);Log("Demons depart.");Award(SingleRoll("Points gained"));break;}
    }
    private void DragonEncounter()
    {
        var s=initialState!;
        if(animal is "Winged Horse" or "Horse" or "Hound"){animal="";animalTurns=0;}
        if(lairDeadline.HasValue)lairDeadline=(lairDeadline.Value+12)%14+1;
        int result=HighRoll("Dragon (§7.305)");
        if(result==4&&s.Rank==4)result=3;
        switch(result)
        {case 1:dragonbound=true;lairDeadline=null;animal="";animalTurns=0;events.Clear();Log("Dragonbound; held Events discarded.");break;
        case 2:lostTurns+=SingleRoll("Turns lost");break;
        case 3:Theft();break;case 4:LosePoints(2*SingleRoll("Point loss"),true);break;
        case 5:if(s.Player==LairCenter()){forcedEncounter=true;repeatEncounter="Dragon";Log("Must encounter Dragon again next turn.");}else Transport(LairCenter());break;
        case 6:Log("Unaffected by Dragon.");break;}
    }
    private void TrapEncounter()
    {
        switch(HighRoll("Trap (§8.3)"))
        {case 1:fortnightTrap=true;Log("Trapped until end of fortnight.");break;
        case 2:Transport(LairCenter());break;
        case 3:lostTurns+=SingleRoll("Turns lost");break;
        case 4:Transport(LairRandom());break;
        case 5:escapeTrap=true;Log("Trapped until white movement die is 5 or 6; then move immediately (2003 correction 3).");break;
        case 6:Transport(RandomLand());break;}
    }
    private void AnimalEncounter()
    {
        int result=HighRoll("Animal (§8.4)");
        if(result==1)
        {
            var s=initialState!;s.Dragon=s.Player;s.DragonLocation=Describe(s.Player);DragonEncounter();return;
        }
        if(Afflicted){Log("Animal will not approach an afflicted player.");return;}
        animal="";animalTurns=0;animalActive=false;
        if(result==6){DrawTask(false);return;}
        animal=result==2?"Unicorn":result==3?"Winged Horse":result==4?"Horse":"Hound";
        animalTurns=result-1;Log($"{animal} available for {animalTurns} turn(s).");
        if(result is 2 or 3){Log("Flight/Unicorn movement awaits implementation; not usable in this preview.");deferredEffects=true;}
    }
    private void ResolveSpecific(Hex h,bool transported=false)
    {
        if(encounterDepth++>=12){encounterDepth--;deferredEffects=true;Log("Encounter chain paused.");return;}
        try
        {
            var s=initialState!;
            if(transported){ignoredTaskHex=null;OfferTaskPickup(h);}
            bool metTask=ResolveTaskMarkers(h);
            bool metHigh=revealedWizards.ContainsKey(h);
            if(metHigh)ResolveRevealed(h);
            if(s.Player!=h)return;
            if(cells.TryGetValue(h,out var c)&&c.Encounter.Length>0)
            {
                string name=c.Encounter;
                bool skipPlace=(metHigh||metTask)&&(name is "Common town" or "Elven dwelling" or "Shrineheart Abbey" or "High Hollow" or "Star Crest");
                if(metTask&&name=="Hamdrel"&&!Afflicted)skipPlace=true;
                if(name=="Shrineheart Abbey"&&Afflicted&&!metHigh)skipPlace=false;
                if(!skipPlace)
                switch(name)
                {
                    case "Common town":CommonFolk();break;
                    case "Elven dwelling":
                        ElvenGift();break;
                    case "Hamdrel":
                        HamdrelGift();break;
                    case "Master Sorcerer":Mentor("Sorcerers",TileLabel(c.Tile),metTask);break;
                    case "Riiktal":Mentor("Druids","Riiktal",metTask);break;
                    case "Shrineheart Abbey":
                        if(Afflicted){Heal();break;}
                        int abbey=HighRoll("Shrineheart Abbey (§7.38)");
                        if(abbey<=2){extraTurns++;Log("One extra turn.");}
                        else if(abbey<=4)
                        {if(s.Order=="Wizards")WizardDraw(true);else if(s.Order!="None")Mentor(s.Order,"Mentor via Abbey");else Log("No Mentor to contact.");}
                        else Award(SingleRoll("Points"));break;
                    case "High Hollow":
                        int hollow=HighRoll("High Hollow (§7.36)");
                        if(hollow<=2)TransportNamed("Star Crest");else if(hollow<=4)TransportNamed("Hamdrel");
                        else {var tower=placed.Where(p=>TileKey(p)=="Magic_2").ToArray();if(tower.Length>0)Transport(tower[0].Center);else {Log("Megmoran's Tower removed; destination resolution pending.");deferredEffects=true;}}break;
                    case "Star Crest":
                        int star=HighRoll("Star Crest (§7.37, corrected)");
                        if(star<=2&&s.Rank==4){Log("Rank 4 choice of High Wizards requires Gem system; pending.");deferredEffects=true;}
                        else if(star<=2){if(s.Rank>0&&!dragonbound){s.Knowledge+=10;s.Perception+=10;s.Power+=10;Log("Gained 10 points of each type.");}}
                        else if(star<=4){WizardDraw(true);WizardDraw(true);}else {extraTurns+=SingleRoll("Extra turns");}break;
                }
            }
            if(s.Player!=h)return;
            if(demons.Contains(h))DemonEncounter();
            if(s.Player!=h)return;
            if(s.Dragon==h||(cells.TryGetValue(h,out var cell)&&cell.Encounter=="Dragon"))DragonEncounter();
        }
        finally{encounterDepth--;}
    }
    private void ResolvePending()
    {
        if(initialState==null||moveStage!=MoveStage.Finished||encounterResolved)return;
        encounterResolved=true;encounterDepth=0;
        Log(pendingEncounter);
        if(pendingEncounter.StartsWith("Specific encounter"))ResolveSpecific(initialState.Player);
        else if(pendingEncounter=="Forced encounter")
        {
            var repeat=repeatEncounter;repeatEncounter="";
            if(repeat=="Common Folk")CommonFolk();else if(repeat=="Dragon")DragonEncounter();
        }
        else if(pendingEncounter=="Random encounter"&&!dragonbound&&!Trapped)
        {
            switch(HighRoll("Random encounter (§8)"))
            {case 1:DemonEncounter();break;case 2:TrapEncounter();break;case 3:AnimalEncounter();break;case 4:DrawEvent();break;case 5:CommonFolk();break;case 6:WizardDraw(false);break;}
        }
        if(cells.TryGetValue(initialState.Player,out var c)&&TileKey(c.Tile)=="Magic_6")
        {
            (initialState.Dragon,initialState.DragonLocation)=RandomSpace(c.Tile);
            Log("Dragon moves within its lair: "+initialState.DragonLocation+".");
            if(initialState.Dragon==initialState.Player)DragonEncounter();
        }
        moveNote=deferredEffects?"Encounter processed; some effects pending. See log/cards.":"Encounter resolved. See log.";
        UpdateStatus();canvas.Invalidate();ShowEncounterLog();
    }
    private void ChooseTestEncounter()
    {
        if(initialState==null || moveStage!=MoveStage.Finished || encounterResolved)return;
        using var f=new Form { Text="Test encounter — changes current preview game",StartPosition=FormStartPosition.CenterParent,ClientSize=new Size(430,200),FormBorderStyle=FormBorderStyle.FixedDialog };
        var kind=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Left=15,Top=15,Width=395};
        kind.Items.AddRange(new object[]{"Random encounter","Common Folk","Demons","Dragon","Trap","Animal","High Wizard (random card)","Hamdrel gift","Elven dwelling","Event card","Task card (choose number)","Event card (choose number)","Mentor: Wizards","Mentor: Sorcerers","Mentor: Druids"});kind.SelectedIndex=0;
        var roll=new NumericUpDown {Minimum=1,Maximum=6,Value=6,Left=15,Top=60,Width=75};
        f.Controls.Add(kind);f.Controls.Add(roll);f.Controls.Add(new Label {Left=100,Top=64,AutoSize=true,Text="Force first table result (1–6)"});
        f.Controls.Add(new Label {Left=15,Top=100,Width=395,Height=40,Text="Testing only: this replaces the pending encounter, even if the location would not normally allow it."});
        var ok=new Button {Text="Run test encounter",Left=15,Top=150,Width=395,DialogResult=DialogResult.OK};f.Controls.Add(ok);f.AcceptButton=ok;
        if(f.ShowDialog(this)!=DialogResult.OK)return;
        encounterResolved=true;deferredEffects=true;encounterDepth=0;testRoll=(int)roll.Value;
        Log("TEST ONLY — replaced "+pendingEncounter+" with "+kind.SelectedItem+".");
        try
        {
            switch((string)kind.SelectedItem!)
            {
                case "Random encounter":switch(HighRoll("Random encounter")){case 1:DemonEncounter();break;case 2:TrapEncounter();break;case 3:AnimalEncounter();break;case 4:DrawEvent();break;case 5:CommonFolk();break;case 6:WizardDraw(false);break;}break;
                case "Common Folk":CommonFolk();break;
                case "Demons":DemonEncounter();break;
                case "Dragon":DragonEncounter();break;
                case "Trap":TrapEncounter();break;
                case "Animal":AnimalEncounter();break;
                case "High Wizard (random card)":WizardDraw(false);break;
                case "Hamdrel gift":HamdrelGift();break;
                case "Elven dwelling":ElvenGift();break;
                case "Event card":DrawEvent();break;
                case "Task card (choose number)":if(SelectTestCard(initialState.TaskDeck,true))DrawTask(false);break;
                case "Event card (choose number)":if(SelectTestCard(initialState.EventDeck,false))DrawEvent();break;
                case "Mentor: Wizards":Mentor("Wizards","High Wizard");break;
                case "Mentor: Sorcerers":Mentor("Sorcerers","Master Sorcerer");break;
                case "Mentor: Druids":Mentor("Druids","Riiktal");break;
            }
        }
        finally{testRoll=null;}
        moveNote="Test encounter processed. See log.";UpdateStatus();canvas.Invalidate();ShowEncounterLog();
    }
    private void ElvenGift()
    {
        int elves=HighRoll("Elven dwelling (§7.39)");
        if(elves<=4)Award(SingleRoll("Points")*(elves<=2?2:1));else Log("Elves elude you.");
    }
    private void HamdrelGift()
    {
        var s=initialState!;
        if(Afflicted){Heal();return;}
        int gift=HighRoll("Hamdrel (§7.353, corrected)");
        if(s.Rank is 0 or 4){if(gift<=2)WizardDraw(true);else if(gift<=4)WizardDraw(false);else WizardDraw(true,true);}
        else {if(gift<=2)Award(2*SingleRoll("Gift points"));else if(gift<=4)DrawTask(false);else WizardDraw(true,true);}
    }
    private void DrawEncounterMarkers(Graphics g)
    {
        using var font=new Font("Segoe UI",18,FontStyle.Bold);
        using var text=new SolidBrush(Color.White);
        void Mark(Hex h,Color color,string label)
        {
            var c=Center(h);using var fill=new SolidBrush(color);
            g.FillEllipse(fill,c.X+15,c.Y-35,35,35);
            g.DrawString(label,font,text,c.X+16,c.Y-34);
        }
        foreach(var h in demons)Mark(h,Color.DarkRed,"D");
        foreach(var h in revealedWizards.Keys)Mark(h,Color.DarkBlue,"W");
        DrawTaskMarkers(g);
    }
    private string EncounterRecord()=> $"Affliction: {(dragonbound?"Dragonbound":demonDazed?"Demon Dazed":"None")} | Lost turns: {lostTurns}\r\n"+
        $"Trap: {(fortnightTrap?"Until fortnight":escapeTrap?"Roll 5 or 6":"None")} | Animal: {(animal==""?"None":animal+" ("+animalTurns+" turns)")}\r\n"+
        $"Tasks held: {tasks.Count} ({tasks.Count(x=>x.Mandatory)} mandatory; {activeTasks.Count} automated) | Events held/pending: {events.Count}\r\n"+
        $"Tasks completed: {initialState!.Completion} | Immediate Events discarded: {eventDiscard.Count}\r\n"+
        $"Demon spaces: {demons.Count} | Revealed Wizard spaces: {revealedWizards.Count}\r\n"+
        $"Pending card or rule effects: {(deferredEffects?"Yes":"None")}\r\n";
}
