namespace Wizards;

internal sealed partial class TerritorySetup
{
    // Card definitions are mechanics transcribed from the supplied card faces.
    // Cards not in these tables remain visibly pending; they are never auto-completed.
    private sealed record TaskDef(int K,int P,int W, string[] Steps, int ObjectStep=-1);
    private sealed record Objective(Hex? Location,string Match,string Label);
    private sealed class ActiveTask
    {
        public string Name="";
        public TaskDef Definition=null!;
        public List<Objective> Steps=new();
        public int Progress;
        public bool HeldObject;
    }
    private readonly Dictionary<string,ActiveTask> activeTasks=new();
    private readonly List<string> taskDiscard=new(),eventDiscard=new();
    private readonly Button taskProgress=new(){Text="Task progress",AutoSize=true,Enabled=false};
    private Hex? ignoredTaskHex;
    private static TaskDef Def(int k,int p,int w,int objectStep,params string[] steps)=>new(k,p,w,steps,objectStep);
    private static readonly Dictionary<int,TaskDef> TaskDefinitions=new()
    {
        [1]=Def(5,5,5,0,"random:Common,Elven","site:Riiktal"),
        [2]=Def(10,5,10,0,"random:Magic_6","site:Hamdrel"),
        [3]=Def(5,0,5,-1,"random:Common,Magic","random:Elven"),
        [4]=Def(0,5,5,0,"random:Elven,Magic","any:Elven dwelling"),
        [5]=Def(5,0,5,0,"exact:Elven_6:B4","mentor"),
        [6]=Def(5,5,0,-1,"random:All","exact:Elven_5:B5"),
        [7]=Def(5,5,5,0,"random:All","exact:Magic_4:Center"),
        [8]=Def(5,5,0,-1,"exact:Common_1:Center","exact:Elven_2:B2"),
        [11]=Def(10,5,0,0,"exact:Common_2:A4","site:Hamdrel"),
        [12]=Def(5,0,10,-1,"exact:Common_5:B5","site:Riiktal"),
        [16]=Def(5,0,5,-1,"random:Common,Elven","exact:Common_4:A4"),
        [17]=Def(5,10,0,-1,"random:Elven,Magic","site:Riiktal"),
        [18]=Def(5,0,5,0,"here","exact:Common_1:Center"),
        [19]=Def(5,10,0,0,"exact:Elven_3:A1","site:Riiktal"),
        [21]=Def(10,5,0,0,"exact:Elven_2:B2","wizard:Aevarex,Terek,Veldor"),
        [29]=Def(10,5,10,1,"random:All","random:Magic_6","random:Common,Elven"),
        [31]=Def(5,10,0,0,"random:All","wizard:Elekov,Zegoral,Veldor"),
        [35]=Def(5,0,10,0,"wizard:Ishkatar,Aevarex,Terek","site:Riiktal"),
        [36]=Def(5,0,5,-1,"exact:Elven_1:C3","exact:Elven_2:B2")
    };
    private static int CardNumber(string name)=>int.TryParse(name.Split('_').Last(),out int n)?n:0;
    private bool IsAutomatedTask(string name)=>TaskDefinitions.ContainsKey(CardNumber(name));
    private string TaskLabel(string name)=>"Task "+CardNumber(name).ToString("000");
    private Hex? PickHex(IEnumerable<Hex> choices,string prompt)
    {
        var options=choices.Distinct().OrderBy(Describe).ToArray();
        if(options.Length==0)return null;
        if(options.Length==1)return options[0];
        using var f=new Form{Text=prompt,StartPosition=FormStartPosition.CenterParent,ClientSize=new Size(555,410),ControlBox=false};
        var list=new ListBox{Dock=DockStyle.Fill};
        foreach(var h in options)list.Items.Add(Describe(h));
        list.SelectedIndex=0;
        var ok=new Button{Text="Choose location",Dock=DockStyle.Bottom,Height=40,DialogResult=DialogResult.OK};
        f.Controls.Add(list);f.Controls.Add(ok);f.AcceptButton=ok;
        f.ShowDialog(this);return options[list.SelectedIndex<0?0:list.SelectedIndex];
    }
    private List<Hex> Candidates(string spec)
    {
        var bits=spec.Split(':');
        if(bits[0]=="exact")return cells.Where(x=>TileKey(x.Value.Tile)==bits[1]&&x.Value.Code==bits[2]).Select(x=>x.Key).ToList();
        if(bits[0] is "site" or "any")return cells.Where(x=>x.Value.Encounter==bits[1]).Select(x=>x.Key).ToList();
        if(bits[0]=="random")
        {
            var categories=bits[1].Split(',');
            return cells.Where(x=>x.Value.Code!="Center"&&categories.Any(cat=>cat=="All"||TileKey(x.Value.Tile)==cat||TileKey(x.Value.Tile).StartsWith(cat+"_"))).Select(x=>x.Key).ToList();
        }
        if(bits[0]=="here")return new(){initialState!.Player};
        return new();
    }
    private bool TaskPossible(TaskDef d)=>d.Steps.All(spec=>spec=="mentor"||spec.StartsWith("wizard:")||Candidates(spec).Count>0);
    private void RegisterTask(string name,TaskDef d,string? source)
    {
        var task=new ActiveTask{Name=name,Definition=d};
        foreach(string spec in d.Steps)
        {
            if(spec=="mentor"){task.Steps.Add(new(null,"mentor","Meet your Mentor"));continue;}
            if(spec.StartsWith("wizard:")){task.Steps.Add(new(null,spec,"Meet "+spec[7..]));continue;}
            var candidates=Candidates(spec);
            Hex h;
            if(spec.StartsWith("random:"))
            {
                // Uniform numbered spaces in the permitted surviving territories (section 4).
                h=candidates[Random.Shared.Next(candidates.Count)];
            }
            else h=PickHex(candidates,"Choose destination — "+TaskLabel(name))!.Value;
            task.Steps.Add(new(h,"",Describe(h)));
        }
        activeTasks.Add(name,task);
        Log($"{TaskLabel(name)}: {string.Join(" → ",task.Steps.Select(x=>x.Label))}.");
        // Section 9.7: first objective can be fulfilled in the assignment encounter.
        if(!dragonbound)
        {
            var first=task.Steps[0];
            if(first.Location==initialState!.Player||MatchesMeeting(first,source))
                AdvanceObjective(task);
        }
    }
    private bool MatchesMeeting(Objective obj,string? source)
    {
        if(source==null||obj.Location.HasValue)return false;
        if(obj.Match=="mentor")return source=="mentor"||(initialState!.Order=="Wizards"&&source.StartsWith("wizard:"));
        return obj.Match.StartsWith("wizard:")&&source.StartsWith("wizard:")&&obj.Match[7..].Split(',').Contains(source[7..]);
    }
    private bool TaskAt(Hex h)=>!dragonbound&&ignoredTaskHex!=h&&activeTasks.Values.Any(t=>t.Progress<t.Steps.Count&&t.Steps[t.Progress].Location==h);
    private bool TaskAuthorizesCrest(Hex h)=>!dragonbound&&activeTasks.Values.Any(t=>t.Progress<t.Steps.Count&&t.Steps[t.Progress].Location==h);
    private bool ResolveTaskMarkers(Hex h)
    {
        if(!TaskAt(h))return false;
        bool any=false;
        foreach(var task in activeTasks.Values.ToArray())
        {
            while(task.Progress<task.Steps.Count&&task.Steps[task.Progress].Location==h)
            {any=true;AdvanceObjective(task);}
        }
        return any;
    }
    private bool ResolveTaskMeeting(string source)
    {
        if(dragonbound)return false;
        bool any=false;
        foreach(var task in activeTasks.Values.ToArray())
        {
            if(task.Progress<task.Steps.Count&&MatchesMeeting(task.Steps[task.Progress],source))
            {any=true;AdvanceObjective(task);}
        }
        return any;
    }
    private void AdvanceObjective(ActiveTask task)
    {
        int step=task.Progress;
        if(step==task.Definition.ObjectStep)task.HeldObject=true;
        Log($"{TaskLabel(task.Name)} objective {step+1}/{task.Steps.Count} fulfilled: {task.Steps[step].Label}.");
        task.Progress++;
        if(task.Progress==task.Steps.Count)CompleteTask(task);
    }
    private void FixedPoints(int k,int p,int w,string reason)
    {
        var s=initialState!;
        if(s.Rank is 0 or 4||dragonbound){Log(reason+": no points gained at this rank or while Dragonbound.");return;}
        s.Knowledge+=k;s.Perception+=p;s.Power+=w;
        Log($"{reason}: +{k} Knowledge, +{p} Perception, +{w} Power.");
    }
    private void CompleteTask(ActiveTask task)
    {
        var s=initialState!;
        FixedPoints(task.Definition.K,task.Definition.P,task.Definition.W,TaskLabel(task.Name));
        activeTasks.Remove(task.Name);tasks.RemoveAll(t=>t.Name==task.Name);taskDiscard.Add(task.Name);
        s.Completion++;
        Log($"{TaskLabel(task.Name)} completed. Completion total: {s.Completion}. Evil Attack thwarting will be connected in a later phase.");
        if(s.Completion%10==0){deferredEffects=true;Log("Ten completed Tasks: Evil thwart credit earned; attack implementation pending.");}
    }
    private void FailStolenTasks()
    {
        foreach(var task in activeTasks.Values.Where(t=>t.HeldObject).ToArray())
        {
            foreach(var step in task.Steps.Skip(task.Progress))
                if(step.Location.HasValue)demons.Add(step.Location.Value);
            activeTasks.Remove(task.Name);tasks.RemoveAll(t=>t.Name==task.Name);taskDiscard.Add(task.Name);
            Log($"{TaskLabel(task.Name)} impossible: magical object stolen. Remaining markers replaced by Demons (§9.8–9.9).");
        }
    }
    private void CompleteByEvent(int count)
    {
        // The printed cards say Tasks already begun. Interpret that as at least one completed objective.
        for(int i=0;i<count;i++)
        {
            var choices=activeTasks.Values.Where(t=>t.Progress>0).OrderBy(t=>t.Name).ToArray();
            if(choices.Length==0)
            {
                Log("No additional automated Task with a completed objective is available.");
                if(tasks.Any(t=>!activeTasks.ContainsKey(t.Name)))deferredEffects=true;
                return;
            }
            using var f=new Form{Text="Choose a begun Task for Event completion",StartPosition=FormStartPosition.CenterParent,ClientSize=new Size(450,250),ControlBox=false};
            var list=new ListBox{Dock=DockStyle.Fill};foreach(var t in choices)list.Items.Add(TaskLabel(t.Name)+$" ({t.Progress}/{t.Steps.Count})");list.SelectedIndex=0;
            var ok=new Button{Text="Complete selected Task",Dock=DockStyle.Bottom,Height=40,DialogResult=DialogResult.OK};
            f.Controls.Add(list);f.Controls.Add(ok);f.AcceptButton=ok;f.ShowDialog(this);
            CompleteTask(choices[Math.Max(0,list.SelectedIndex)]);
        }
    }
    private bool ApplyEventCard(string name)
    {
        int n=CardNumber(name);
        // Immediate cards verified against printed faces. Remaining cards stay pending.
        switch(n)
        {
            case 2:CompleteByEvent(2);break;
            case 3:case 14:case 15:FixedPoints(2*SingleRoll("Knowledge award"),0,0,"Event "+n);break;
            case 4:case 22:if(FindEncounter("Star Crest") is not Hex crest)return false;Transport(crest);break;
            case 5:extraTurns+=2;Log("Event 05: two extra turns.");break;
            case 6:case 16:CompleteByEvent(1);break;
            case 7:case 12:FixedPoints(0,2*SingleRoll("Perception award"),0,"Event "+n);break;
            case 8:Award(2*SingleRoll("Point award"));break;
            case 10:case 13:FixedPoints(0,0,2*SingleRoll("Power award"),"Event "+n);break;
            case 11:if(FindEncounter("High Hollow") is not Hex hollow)return false;Transport(hollow);break;
            case 17:extraTurns+=SingleRoll("Extra turns");break;
            case 19:extraTurns++;Log("Event 19: one extra turn.");break;
            case 20:FixedPoints(5,5,5,"Event 20");break;
            case 21:
                var destination=PickHex(cells.Where(x=>TileKey(x.Value.Tile).StartsWith("Elven_")).Select(x=>x.Key),"Event 21 — select Elven destination");
                if(!destination.HasValue)return false;
                Transport(destination.Value);break;
            default:return false;
        }
        return true;
    }
    private void DrawTaskMarkers(Graphics g)
    {
        using var font=new Font("Segoe UI",15,FontStyle.Bold);
        using var pen=new Pen(Color.White,2);
        using var ink=new SolidBrush(Color.White);
        foreach(var group in activeTasks.Values.SelectMany(t=>t.Steps.Skip(t.Progress).Where(s=>s.Location.HasValue).Select(s=>(H:s.Location!.Value,Ready:t.Steps[t.Progress]==s))).GroupBy(x=>x.H))
        {
            var center=Center(group.Key);
            using var fill=new SolidBrush(group.Any(x=>x.Ready)?Color.DarkGreen:Color.DimGray);
            var rect=new RectangleF(center.X-43,center.Y+7,35,28);
            g.FillRectangle(fill,rect);g.DrawRectangle(pen,rect.X,rect.Y,rect.Width,rect.Height);
            g.DrawString(group.Count()>1?"T"+group.Count():"T",font,ink,rect.X+1,rect.Y+1);
        }
    }
    private void ShowTaskProgress()
    {
        using var f=new Form{Text="Wizards — Task progress",StartPosition=FormStartPosition.CenterParent,ClientSize=new Size(700,500)};
        var lines=new List<string>();
        foreach(var task in tasks)
        {
            lines.Add((task.Mandatory?"Mandatory ":"Optional ")+TaskLabel(task.Name));
            if(activeTasks.TryGetValue(task.Name,out var t))
            {
                for(int i=0;i<t.Steps.Count;i++)lines.Add($"  {(i<t.Progress?"Completed":i==t.Progress?"Next":"Later")} — {t.Steps[i].Label}");
                if(t.HeldObject)lines.Add("  Carrying theft-sensitive magical object.");
            }
            else lines.Add("  Card-specific automation pending — view original card.");
            lines.Add("");
        }
        lines.Add($"Completed/discarded Tasks: {taskDiscard.Count}. Completion total: {initialState!.Completion}.");
        lines.Add("Green T = eligible objective; gray T = prerequisite not yet met.");
        var text=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Text=string.Join(Environment.NewLine,lines),Font=new Font("Segoe UI",11)};
        f.Controls.Add(text);f.ShowDialog(this);
    }
    private bool SelectTestCard(List<string> deck,bool task)
    {
        using var f=new Form{Text="Select available card — TEST ONLY",StartPosition=FormStartPosition.CenterParent,ClientSize=new Size(460,350)};
        var list=new ListBox{Dock=DockStyle.Fill};
        var cards=deck.OrderBy(CardNumber).ToList();
        if(cards.Count==0){Log("No cards left in deck.");return false;}
        foreach(var card in cards)list.Items.Add(card+(task?(IsAutomatedTask(card)?" (automated)":" (pending)"):""));
        list.SelectedIndex=0;
        var ok=new Button{Text="Draw selected card",Dock=DockStyle.Bottom,Height=40,DialogResult=DialogResult.OK};f.Controls.Add(list);f.Controls.Add(ok);f.AcceptButton=ok;
        if(f.ShowDialog(this)!=DialogResult.OK)return false;
        string selected=cards[list.SelectedIndex];deck.Remove(selected);deck.Insert(0,selected);
        Log("TEST ONLY — selected "+selected+".");return true;
    }
}
