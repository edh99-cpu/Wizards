namespace Wizards;

internal sealed partial class TerritorySetup
{
    // Card definitions are mechanics transcribed from the supplied card faces.
    // Cards not in these tables remain visibly pending; they are never auto-completed.
    private sealed record TaskDef(int K,int P,int W, string[] Steps, int ObjectStep=-1);
    private sealed record Objective(Hex? Location,string Match,string Label,int Phase,bool Object);
    private sealed class ActiveTask
    {
        public string Name="";
        public TaskDef Definition=null!;
        public List<Objective> Steps=new();
        public int Progress;
        public bool HeldObject;
        public string? FirstWizard;
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
        [36]=Def(5,0,5,-1,"exact:Elven_1:C3","exact:Elven_2:B2"),
        [27]=Def(5,5,5,0,"mentor","exact:Common_6:C2","exact:Elven_2:A1"),
        [32]=Def(0,10,0,0,"coastal:Magic","exact:Elven_6:Center"),
        [37]=Def(5,5,0,0,"random:Elven","exact:Elven_3:A1"),
        [38]=Def(5,0,10,0,"random:Magic_4","wizard:Ishkatar,Aevarex,Elekov,Terek,Tolmitar,Veldor,Zegoral"),
        [40]=Def(10,5,0,0,"exact:Magic_2:Center","exact:Elven_4:A4"),
        [41]=Def(0,0,10,-1,"random:Magic","exact:Elven_3:A1"),
        [42]=Def(0,10,5,-1,"random:Common,Elven","site:Hamdrel"),
        [46]=Def(5,5,5,-1,"random:Magic_3","wizard:Tolmitar,Zegoral,Terek,Veldor"),
        [47]=Def(0,5,10,-1,"site:Shrineheart Abbey","site:Riiktal"),
        [48]=Def(10,0,0,-1,"exact:Common_6:C6","exact:Common_4:C2"),
        [49]=Def(0,5,5,-1,"exact:Common_3:B1","site:Shrineheart Abbey"),
        [50]=Def(5,0,10,-1,"exact:Elven_6:Center","exact:Elven_2:B2","exact:Elven_3:A1"),
        [51]=Def(0,10,0,-1,"random:Common,Elven","exact:Common_5:B5"),
        [54]=Def(10,10,10,0,"exact:Magic_6:Center","exact:Magic_2:Center"),
        [55]=Def(5,5,0,-1,"random:All","site:Shrineheart Abbey"),
        [56]=Def(5,10,10,-1,"random:Magic_6","anysettlement"),
        [57]=Def(0,5,5,-1,"exact:Elven_1:C3","site:Star Crest"),
        [58]=Def(0,5,10,-1,"random:All","exact:Magic_3:Center"),
        [59]=Def(0,5,5,-1,"random:Common,Elven","exact:Elven_2:B2"),
        [63]=Def(5,5,0,-1,"exact:Common_5:A2","site:High Hollow"),
        [64]=Def(10,5,10,0,"random:NotLair","random:Magic_6"),
        [65]=Def(10,10,5,0,"random:Magic_6","exact:Elven_1:C3"),
        [66]=Def(0,5,5,0,"exact:Elven_5:B5","random:All"),
        [67]=Def(5,0,5,-1,"exact:Elven_4:A4","random:Common,Elven"),
        // & = same prerequisite phase; * = collection of a theft-sensitive object.
        [10]=Def(5,0,10,-1,"*exact:Magic_2:Center","&*exact:Magic_3:Center","farthestwater"),
        [13]=Def(5,5,5,-1,"mentor","anysettlement","&anysettlement","&anysettlement"),
        [14]=Def(10,5,5,-1,"*exact:Magic_4:Center","&*exact:Elven_3:A1","&*exact:Common_1:Center","site:Shrineheart Abbey"),
        [15]=Def(5,10,10,-1,"all:stones","site:Riiktal"),
        [22]=Def(5,5,10,0,"site:Riiktal","random:Magic_2","&random:Magic_3","&random:Magic_4","&random:Magic_5"),
        [23]=Def(5,10,5,-1,"exact:Elven_6:B3","wizard:any","&any:Master Sorcerer","&site:Hamdrel"),
        [24]=Def(5,5,5,-1,"*random:Magic","&*any:Common town","site:Star Crest"),
        [25]=Def(10,5,10,0,"wizard:any","all:sorcerers"),
        [26]=Def(10,5,0,0,"wizard:any","wizard:other"),
        [28]=Def(5,5,0,0,"random:Common,Magic","any:Elven dwelling","&any:Elven dwelling","&any:Elven dwelling"),
        [30]=Def(5,5,5,-1,"mentor","wizard:any","&site:Hamdrel"),
        [33]=Def(5,5,5,-1,"random:All","exact:Elven_1:C3","&site:Star Crest"),
        [39]=Def(10,0,5,0,"site:Riiktal","site:Star Crest","&site:High Hollow"),
        [43]=Def(0,5,10,0,"terrain:Common:Woods","all:inlandtowns"),
        [44]=Def(10,5,5,0,"exact:Elven_6:Center","all:randomelvenplains"),
        [53]=Def(5,10,10,0,"all:elvenwoods","exact:Elven_6:Center"),
        [60]=Def(5,10,10,-1,"site:Riiktal","all:elves"),
        [61]=Def(10,10,5,-1,"*terrain:Magic:Any","&*terrain:Magic:Any","&*terrain:Magic:Any","&*terrain:Magic:Any","&*terrain:Magic:Any","&*terrain:Magic:Any","site:Hamdrel"),
        [62]=Def(10,0,5,0,"coastal:Elven dwelling","wizard:Aevarex,Zegoral,Terek"),
        [68]=Def(0,5,5,-1,"random:All","water:2"),
        [69]=Def(5,5,10,-1,"exact:Magic_4:Center","exact:Magic_3:Center","exact:Magic_2:Center"),
        [70]=Def(5,0,5,-1,"random:Common,Magic","any:Elven dwelling"),
        [71]=Def(5,5,5,0,"coastal:All","site:Star Crest","&site:High Hollow","&site:Shrineheart Abbey"),
        [72]=Def(5,10,5,-1,"terrain:Magic:Any","all:elves"),
        [73]=Def(0,5,10,-1,"*random:Common_1","&*random:Common_1","&*random:Common_1","&*random:Common_1","mentor"),
        [74]=Def(5,5,5,-1,"*terrain:All:Mountain","&*terrain:Common:Plain","&*terrain:Elven:Woods","&*water:1","site:Hamdrel"),
        [75]=Def(10,0,5,0,"site:Hamdrel","farthestsettlement"),
        [76]=Def(5,10,0,-1,"*coastal:All","&*terrain:All:Mountain","&*terrain:All:Woods","wizard:any"),
        [77]=Def(0,5,10,-1,"random:All","any:Elven dwelling","site:Riiktal"),
        [78]=Def(5,10,0,1,"exact:Common_2:B3","random:Elven","wizard:any"),
        [79]=Def(5,5,5,-1,"random:All","site:High Hollow"),
        [80]=Def(5,10,0,0,"site:Hamdrel","exact:Elven_6:Center"),
        [81]=Def(5,0,10,0,"random:All","exact:Magic_2:Center"),
        [82]=Def(0,5,5,-1,"random:All","site:Shrineheart Abbey"),
        [83]=Def(10,5,0,-1,"random:Common,Elven","wizard:any"),
        [84]=Def(5,5,5,-1,"random:Elven_3","site:Hamdrel"),
        [85]=Def(5,0,10,-1,"exact:Common_2:A1","exact:Magic_4:Center"),
        [86]=Def(5,10,0,-1,"coastal:Common town","site:Riiktal"),
        [87]=Def(5,5,5,0,"exact:Elven_2:B2","water:3","exact:Elven_1:C3"),
        [88]=Def(10,10,5,-1,"site:Riiktal","wizard:any","&any:Master Sorcerer","&site:Hamdrel"),
        [89]=Def(5,0,10,0,"exact:Magic_3:Center","exact:Common_6:C6"),
        [90]=Def(0,5,10,0,"site:Riiktal","wizard:Ishkatar,Tolmitar,Aevarex,Elekov"),
        [91]=Def(5,5,5,0,"random:All","site:Riiktal"),
        [92]=Def(5,0,5,0,"random:Magic_1","site:Star Crest"),
        [93]=Def(5,10,0,0,"site:Shrineheart Abbey","site:Riiktal"),
        [94]=Def(5,0,10,-1,"random:All","site:Hamdrel","exact:Common_4:C2"),
        [95]=Def(10,5,10,0,"mentor","all:coastalsettlements"),
        [96]=Def(0,5,5,0,"exact:Common_5:A2","exact:Elven_4:A4"),
        [97]=Def(5,10,0,-1,"terrain:All:Mountain","wizard:Ishkatar,Tolmitar,Elekov,Zegoral"),
        [98]=Def(5,0,5,-1,"random:All","exact:Elven_5:B5")
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
        foreach(var h in options)list.Items.Add(ObjectiveLabel(h));
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
        if(bits[0]=="anysettlement")return cells.Where(x=>x.Value.Encounter is "Common town" or "Elven dwelling").Select(x=>x.Key).ToList();
        if(bits[0]=="coastal")return cells.Where(x=>(bits[1]=="All"||x.Value.Encounter==bits[1]||TileKey(x.Value.Tile).StartsWith(bits[1]+"_"))&&IsCoastal(x.Key)).Select(x=>x.Key).ToList();
        if(bits[0]=="terrain")return cells.Where(x=>(bits[1]=="All"||TileKey(x.Value.Tile).StartsWith(bits[1]+"_"))&&(bits[2]=="Any"||x.Value.Kind.ToString()==bits[2])).Select(x=>x.Key).ToList();
        if(bits[0]=="water")return WaterHexes().Where(h=>WaterDistance(h)>=int.Parse(bits[1])).ToList();
        if(bits[0]=="farthestwater"){var water=WaterHexes().ToArray();if(water.Length==0)return new();int far=water.Max(WaterDistance);return water.Where(h=>WaterDistance(h)==far).ToList();}
        if(bits[0]=="farthestsettlement"){var home=FindEncounter("Hamdrel");var sites=Candidates("anysettlement");if(!home.HasValue||sites.Count==0)return new();int far=sites.Max(h=>HexDistance(home.Value,h));return sites.Where(h=>HexDistance(home.Value,h)==far).ToList();}
        if(bits[0]=="all")return bits[1] switch
        {
            "stones"=>cells.Where(x=>x.Value.Kind==Terrain.Stone).Select(x=>x.Key).ToList(),
            "sorcerers"=>Candidates("site:Master Sorcerer"),
            "elves"=>Candidates("site:Elven dwelling"),
            "inlandtowns"=>Candidates("site:Common town").Where(h=>!IsCoastal(h)).ToList(),
            "coastalsettlements"=>Candidates("anysettlement").Where(IsCoastal).ToList(),
            "randomelvenplains"=>cells.Where(x=>TileKey(x.Value.Tile).StartsWith("Elven_")&&TileKey(x.Value.Tile)!="Elven_6"&&x.Value.Kind==Terrain.Plain).Select(x=>x.Key).ToList(),
            "elvenwoods"=>cells.Where(x=>TileKey(x.Value.Tile).StartsWith("Elven_")&&x.Value.Kind==Terrain.Woods).Select(x=>x.Key).ToList(),
            _=>new List<Hex>()
        };
        if(bits[0]=="random")
        {
            var categories=bits[1].Split(',');
            return cells.Where(x=>x.Value.Code!="Center"&&categories.Any(cat=>cat=="All"||(cat=="NotLair"&&TileKey(x.Value.Tile)!="Magic_6")||TileKey(x.Value.Tile)==cat||TileKey(x.Value.Tile).StartsWith(cat+"_"))).Select(x=>x.Key).ToList();
        }
        if(bits[0]=="here")return new(){initialState!.Player};
        return new();
    }
    private static string Bare(string spec)=>spec.TrimStart('&','*');
    private static int HexDistance(Hex a,Hex b)
    {int q=a.Q-b.Q,r=a.R-b.R;return Math.Max(Math.Abs(q),Math.Max(Math.Abs(r),Math.Abs(q+r)));}
    private bool IsCoastal(Hex h)=>Clockwise.Any(d=>OnBoard(Add(h,d))&&!cells.ContainsKey(Add(h,d)));
    private IEnumerable<Hex> WaterHexes()
    {
        int maxQ=(int)Math.Ceiling(board.Width/Dx)+2,maxR=(int)Math.Ceiling(board.Height/Dy)+2;
        for(int q=0;q<=maxQ;q++)for(int r=-maxQ;r<=maxR;r++){var h=new Hex(q,r);if(OnBoard(h)&&!cells.ContainsKey(h))yield return h;}
    }
    private int WaterDistance(Hex h)=>cells.Keys.Min(land=>HexDistance(land,h));
    private string ObjectiveLabel(Hex h)=>cells.ContainsKey(h)?Describe(h):$"Water (Q {h.Q}, R {h.R}; {WaterDistance(h)} from land)";
    private bool TaskPossible(TaskDef d)
    {
        foreach(var group in d.Steps.Select(Bare).GroupBy(s=>s))
        {
            string s=group.Key;if(s=="mentor"||s.StartsWith("wizard:"))continue;
            if(Candidates(s).Count<group.Count())return false;
        }
        return true;
    }
    private IEnumerable<List<Hex>> ElvenWoodGroups()
    {
        var remaining=Candidates("all:elvenwoods").ToHashSet();
        while(remaining.Count>0)
        {
            var h=remaining.First();remaining.Remove(h);
            var group=new List<Hex>{h};
            for(int i=0;i<group.Count;i++)
                foreach(var d in Clockwise){var next=Add(group[i],d);if(remaining.Remove(next))group.Add(next);}
            yield return group;
        }
    }
    private void RegisterTask(string name,TaskDef d,string? source)
    {
        var task=new ActiveTask{Name=name,Definition=d};
        int phase=-1;
        for(int i=0;i<d.Steps.Length;i++)
        {
            string raw=d.Steps[i],spec=Bare(raw);
            if(!raw.StartsWith("&"))phase++;
            bool obj=raw.Contains('*')||i==d.ObjectStep;
            void AddHex(Hex h)=>task.Steps.Add(new(h,"",ObjectiveLabel(h),phase,obj));
            if(spec=="mentor"){task.Steps.Add(new(null,"mentor","Meet your Mentor",phase,obj));continue;}
            if(spec.StartsWith("wizard:")){task.Steps.Add(new(null,spec,spec=="wizard:any"?"Meet any High Wizard":spec=="wizard:other"?"Meet a different High Wizard":"Meet "+spec[7..],phase,obj));continue;}
            if(spec=="all:elvenwoods")
            {
                foreach(var wood in ElvenWoodGroups())AddHex(PickHex(wood,"Choose a space in each continuous Elven wood")!.Value);
                continue;
            }
            var candidates=Candidates(spec);
            if(spec=="all:randomelvenplains")
            {
                var groups=candidates.GroupBy(h=>cells[h].Tile).ToArray();
                foreach(var h in groups[Random.Shared.Next(groups.Length)])AddHex(h);
                continue;
            }
            if(spec.StartsWith("all:")){foreach(var h in candidates.OrderBy(ObjectiveLabel))AddHex(h);continue;}
            if(raw.StartsWith("&")&&d.Steps.Take(i).Select(Bare).Contains(spec))
                candidates=candidates.Where(h=>!task.Steps.Any(x=>x.Phase==phase&&x.Location==h)).ToList();
            if(candidates.Count==0){Log(TaskLabel(name)+": insufficient distinct legal objectives; set aside.");tasks.RemoveAll(t=>t.Name==name);taskDiscard.Add(name);deferredEffects=true;return;}
            var dest=spec.StartsWith("random:")?candidates[Random.Shared.Next(candidates.Count)]:PickHex(candidates,"Choose destination — "+TaskLabel(name))!.Value;
            AddHex(dest);
        }
        activeTasks.Add(name,task);
        Log(TaskLabel(name)+": "+string.Join(" | ",task.Steps.GroupBy(x=>x.Phase).Select(g=>string.Join(" + ",g.Select(x=>x.Label))))+".");
        if(!dragonbound&&!TaskSuspended())
        {
            int firstPhase=task.Steps[0].Phase;
            while(activeTasks.ContainsKey(name))
            {
                int ix=ReadyIndices(task).FirstOrDefault(j=>task.Steps[j].Phase==firstPhase&&(task.Steps[j].Location==initialState!.Player||MatchesMeeting(task,task.Steps[j],source)),-1);
                if(ix<0)break;
                AdvanceObjective(task,ix,source);
            }
        }
    }
    private bool MatchesMeeting(ActiveTask task,Objective obj,string? source)
    {
        if(source==null||obj.Location.HasValue)return false;
        if(obj.Match=="mentor")return source=="mentor"||(initialState!.Order=="Wizards"&&source.StartsWith("wizard:"));
        if(!source.StartsWith("wizard:")||!obj.Match.StartsWith("wizard:"))return false;
        return obj.Match=="wizard:any"||(obj.Match=="wizard:other"&&task.FirstWizard!=null&&task.FirstWizard!=source)||obj.Match[7..].Split(',').Contains(source[7..]);
    }
    private IEnumerable<int> ReadyIndices(ActiveTask t)
    {
        if(t.Progress>=t.Steps.Count)yield break;
        int phase=t.Steps[t.Progress].Phase;
        for(int i=t.Progress;i<t.Steps.Count&&t.Steps[i].Phase==phase;i++)yield return i;
    }
    private bool TaskAt(Hex h)=>!dragonbound&&!TaskSuspended()&&ignoredTaskHex!=h&&activeTasks.Values.Any(t=>ReadyIndices(t).Any(i=>t.Steps[i].Location==h));
    private bool TaskAuthorizesCrest(Hex h)=>!dragonbound&&!TaskSuspended()&&activeTasks.Values.Any(t=>ReadyIndices(t).Any(i=>t.Steps[i].Location==h));
    private void OfferTaskPickup(Hex h)
    {
        if(!TaskAt(h))return;
        var labels=activeTasks.Values.Where(t=>ReadyIndices(t).Any(i=>t.Steps[i].Location==h)).Select(t=>TaskLabel(t.Name));
        if(MessageBox.Show(this,"Fulfill eligible Task objective(s) here?\r\n"+string.Join(", ",labels)+"\r\nNo leaves the Task Markers in place.","Task Marker",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)ignoredTaskHex=h;
    }
    private bool ResolveTaskMarkers(Hex h)
    {
        if(!TaskAt(h))return false;
        bool any=false;
        foreach(var task in activeTasks.Values.ToArray())
        {
            while(activeTasks.ContainsKey(task.Name))
            {
                int index=ReadyIndices(task).FirstOrDefault(i=>task.Steps[i].Location==h,-1);
                if(index<0)break;
                any=true;AdvanceObjective(task,index,null);
            }
        }
        return any;
    }
    private bool ResolveTaskMeeting(string source)
    {
        if(dragonbound||TaskSuspended())return false;
        bool any=false;
        foreach(var task in activeTasks.Values.ToArray())
        {
            int index=ReadyIndices(task).FirstOrDefault(i=>MatchesMeeting(task,task.Steps[i],source),-1);
            if(index>=0){any=true;AdvanceObjective(task,index,source);}
        }
        return any;
    }
    private void AdvanceObjective(ActiveTask task,int index,string? source)
    {
        int step=task.Progress;
        var objective=task.Steps[index];
        if(index!=step)(task.Steps[index],task.Steps[step])=(task.Steps[step],task.Steps[index]);
        if(objective.Object)task.HeldObject=true;
        if(source?.StartsWith("wizard:")==true&&task.FirstWizard==null)task.FirstWizard=source;
        Log($"{TaskLabel(task.Name)} objective {step+1}/{task.Steps.Count} fulfilled: {objective.Label}.");
        task.Progress++;
        if(task.Progress==task.Steps.Count)CompleteTask(task);
    }
    private void FixedPoints(int k,int p,int w,string reason)
    {
        var s=initialState!;
        string? denial=PointDenial();if(denial!=null){Log(reason+": "+denial);return;}
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
        if(TaskSuspended()||dragonbound){Log("Tasks cannot be completed while summoned or Dragonbound.");return;}
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
    private string? PointDenial()=>dragonbound?"No points gained: Dragonbound.":initialState!.Rank==0?"No points gained: you have not joined a Magical Order (Rank 0).":initialState.Rank==4?"No points gained: maximum rank (Rank 4) reached.":null;
    private bool ApplyEventCard(string name)
    {
        int n=CardNumber(name);
        // Immediate cards verified against printed faces. Remaining cards stay pending.
        switch(n)
        {
            case 1:
                var chosen=PickHex(cells.Where(x=>x.Value.Kind is not (Terrain.CircleCenter or Terrain.Crest)).Select(x=>x.Key).Concat(WaterHexes()),"Event 01 — choose destination");
                if(!chosen.HasValue)return false;Transport(chosen.Value);break;
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
            case 23:
                int gift=2*SingleRoll("Perception and Power award");FixedPoints(0,gift,gift,"Event 23");HoldEvent(name);break;
            case 27:case 29:case 30:case 31:HoldEvent(name);break;
            case 32:HoldTimedEvent(name,14,true);break;
            case 37:HoldMovementEvent(name,2);break;
            case 25:case 28:HoldTimedEvent(name,14,true);break;
            case 34:case 40:case 42:HoldMovementEvent(name,2);break;
            case 35:case 36:case 38:case 39:case 41:HoldMovementEvent(name,7);break;
            case 43:HoldMovementEvent(name,3);break;
            case 45:case 46:case 47:CancelMovementEvent("Event trap");lostTurns+=SingleRoll("Turns lost");break;
            case 49:CancelMovementEvent("Event trap");fortnightTrap=true;Log("Event 49: trapped until the end of this fortnight.");break;
            default:return false;
        }
        return true;
    }
    private void DrawTaskMarkers(Graphics g)
    {
        using var font=new Font("Segoe UI",15,FontStyle.Bold);
        using var pen=new Pen(Color.White,2);
        using var ink=new SolidBrush(Color.White);
        foreach(var group in activeTasks.Values.SelectMany(t=>t.Steps.Skip(t.Progress).Where(s=>s.Location.HasValue).Select(s=>(H:s.Location!.Value,Ready:!dragonbound&&!TaskSuspended()&&s.Phase==t.Steps[t.Progress].Phase))).GroupBy(x=>x.H))
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
                for(int i=0;i<t.Steps.Count;i++)lines.Add($"  {(i<t.Progress?"Completed":ReadyIndices(t).Contains(i)&&!TaskSuspended()&&!dragonbound?"Next":"Later")} — {t.Steps[i].Label}");
                if(t.HeldObject)lines.Add("  Carrying theft-sensitive magical object.");
            }
            else lines.Add("  Card-specific automation pending — view original card.");
            lines.Add("");
        }
        lines.Add($"Completed/discarded Tasks: {taskDiscard.Count}. Completion total: {initialState!.Completion}.");
        lines.Add("Green T = eligible objective (any order within the current phase); gray T = not yet eligible.");
        if(TaskSuspended())lines.Add("Task activity suspended until you answer Event 31 summons.");
        var text=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Text=string.Join(Environment.NewLine,lines),Font=new Font("Segoe UI",11)};
        f.Controls.Add(text);f.ShowDialog(this);
    }
    private bool SelectTestCard(List<string> deck,bool task)
    {
        using var f=new Form{Text="Select available card — TEST ONLY",StartPosition=FormStartPosition.CenterParent,ClientSize=new Size(460,350)};
        var list=new ListBox{Dock=DockStyle.Fill};
        var cards=deck.OrderBy(CardNumber).ToList();
        if(cards.Count==0){Log("No cards left in deck.");return false;}
        foreach(var card in cards)list.Items.Add(card+(task?(IsAutomatedTask(card)?" (automated)":" (pending)"):(IsAutomatedEvent(CardNumber(card))?" (automated)":" (pending)")));
        list.SelectedIndex=0;
        var ok=new Button{Text="Draw selected card",Dock=DockStyle.Bottom,Height=40,DialogResult=DialogResult.OK};f.Controls.Add(list);f.Controls.Add(ok);f.AcceptButton=ok;
        if(f.ShowDialog(this)!=DialogResult.OK)return false;
        string selected=cards[list.SelectedIndex];deck.Remove(selected);deck.Insert(0,selected);
        Log("TEST ONLY — selected "+selected+".");return true;
    }
}
