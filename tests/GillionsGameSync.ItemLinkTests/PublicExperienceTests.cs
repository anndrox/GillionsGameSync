using GillionsGameSync;

internal static class PublicExperienceTests {
    internal static void Run() {
        int checks=0;
        void Check(bool ok,string why) { checks++; if(!ok) throw new Exception(why); }
        var state=new HuntProgressState();
        HuntProgressSnapshot Snapshot(long clock,int kills=1,bool coverage=true,uint territory=100) =>
            new(territory,"Current area","",coverage,[new("bill:order:target","An exceptionally long localized-like target name",kills,3)],clock+6000);
        state.Update(false,100,Snapshot(0),0); Check(!state.Visible,"default OFF never auto-shows");
        state.Update(true,100,Snapshot(0),0); Check(state.Visible,"enable current-area auto-show");
        Check(state.Display.Rows.Single().Remaining==2,"remaining is primary, no subtraction needed");
        state.Close(); state.Update(true,100,Snapshot(1000,2),1000); Check(!state.Visible,"progress never defeats manual close");
        state.Update(true,100,Snapshot(2000,2),2000); Check(!state.Visible,"reconnect/UI refresh cannot defeat manual close");
        state.Update(true,101,Snapshot(3000,2,true,101),3000); Check(state.Visible,"new territory re-enables auto-show");
        state.Update(true,101,Snapshot(4000,3,true,101),4000);
        Check(state.Display.Rows.Single().Complete,"new completion holds a text row");
        Check(state.Display.Status.Contains("complete"),"all complete needs verified coverage");
        state.Update(true,101,Snapshot(6400,3,true,101),6400); Check(state.Visible,"completion is held for 2.5 seconds");
        state.Update(true,101,Snapshot(6500,3,true,101),6500); Check(!state.Visible && state.Display.Rows.Length==0,"completed row removed and auto-window hidden");
        state.Open(); state.Update(true,101,Snapshot(7000,3,true,101),7000); Check(state.Visible,"manual reopen remains open");
        state.Update(true,101,Snapshot(7100,3,false,101),7100); Check(state.Display.Status == "The Hunt targets you can see here are complete."&&!state.Display.Status.Contains("All current"),"fresh completed rows do not imply full current coverage or an outstanding read");
        state.Update(true,101,new(101,"Current area","",false,[],13200),7200);
        Check(state.Visible&&state.Display.Status=="None of your Hunt targets are in this zone."&&state.Display.Rows.Length==0,"manual open with fresh empty current-area rows is explicit, not updating or no bills");
        state.Update(true,101,new(101,"Current area","No current Hunt targets could be verified from this read. Bill state is unknown.",false,[],13300),7300);
        Check(state.Display.Status.Contains("Bill state is unknown")&&!state.Display.Status.Contains("updating"),"no admitted source observations are unknown, not an indefinitely pending update");
        state.Update(true,101,Snapshot(7400,1,false,101),7400);
        Check(state.Display.Status.Length==0&&state.Display.Rows.Single().Current==1,"current-area rows recover after a source gap without stale completion");
        state.Update(true,101,Snapshot(7400,1,false,102),7500);
        Check(state.Display.Status.Contains("updating")&&state.Display.Rows.Length==0,"different territory observations cannot masquerade as fresh current-area coverage");
        state.Update(true,101,Snapshot(0,2,true,101),8000); Check(state.Display.Rows.Length==0,"expired sample never displays stale numbers");
        state.Update(true,101,HuntProgressSnapshot.Unavailable,8000); Check(state.Display.Status.Contains("unavailable"),"unavailable is explicit");
        state.Update(false,101,Snapshot(8000,2,true,101),8000); Check(!state.Visible,"disable hides without local-history mutation");
        state.Update(true,100,Snapshot(8100),8100);state.Close();state.Invalidate();
        state.Update(true,100,Snapshot(8200,2),8200);Check(!state.Visible,"session reconnect preserves same-territory manual suppression");
        state.Invalidate(endTerritorySession:true);
        state.Update(true,100,Snapshot(8300,3),8300);Check(!state.Visible&&state.Display.Rows.Length==0,"new character cannot inherit a completion transition");
        state.Update(true,100,Snapshot(8400),8400);Check(state.Visible,"new character current incomplete observations may auto-show");
        state.Update(true,100,HuntProgressSnapshot.Unavailable,8500);
        state.Update(true,100,Snapshot(8600,3),8600);Check(state.Display.Rows.Length==0&&!state.Display.Status.Contains("complete"),"unavailable gap cannot prove a completion transition");
        state.Update(true,100,Snapshot(8700),8700);state.Update(true,100,Snapshot(8800,3),8800);
        state.Update(true,100,new(100,"Current area","",false,[new("new-bill","New target",0,1)],15000),8900);
        Check(state.Display.Rows.Length==1&&state.Display.Rows[0].Key=="new-bill","removed or replaced bill cannot retain a completion row");
        CheckHuntOrdering(Check);
        var gate=new HuntProgressWindowGate();
        Check(gate.Prepare(true,0),"visible model may open standard window");
        Check(gate.Observe(true,false,0),"post-draw user close invokes model dismissal once");
        Check(!gate.Prepare(true,0),"queued close cannot reopen on the next UI frame");
        Check(!gate.Observe(false,false,0),"programmatic auto-hide is not a manual dismissal");
        Check(gate.Prepare(true,2),"explicit model reopen releases window latch");
        Check(!gate.Observe(true,true,2),"normal drawing never closes model");
        Check(!gate.Prepare(false,2)&&gate.Prepare(true,2),"automatic hiding permits later incomplete target in same territory");
        gate.Observe(true,false,state.InteractionRevision);state.Close();
        state.Update(true,102,Snapshot(9000,1,true,102),9000);
        Check(gate.Prepare(state.Visible,state.InteractionRevision),"territory reset releases actual window dismissal latch");
        Check(PublicHealth.ConnectionState(false,false,"")=="Not connected","unpaired UX");
        Check(PublicHealth.ConnectionState(true,false,"DEVICE_INVALID").Contains("revoked"),"revocation UX");
        Check(PublicHealth.ConnectionState(true,false,"VERSION_UNSUPPORTED")=="Update required","version UX");
        Check(PublicHealth.ConnectionState(false,true,"")=="Connecting","pairing UX");
        foreach(string marker in new[]{"synthetic-bearer","synthetic-pairing","synthetic-cookie","synthetic-header","private-hunt-json","private-market-json","account-id-123"}) {
            var health=new PublicHealth(marker,marker,marker,marker,marker,marker,null,marker,marker,marker);
            Check(!health.SupportSummary().Contains(marker),"copy summary whitelist: "+marker);
            Check(PublicConnectionPresentation.ActionMessage(marker).Length==0,"action feedback excludes raw secret: "+marker);
        }
        Check(PublicConnectionPresentation.PairingUrl("https://example.invalid/")=="https://example.invalid/gillions-sync#pairing","saved next origin is the exact displayed/clicked pairing recipient");
        Check(PublicConnectionPresentation.PairingUrl("https://test.gillions.app")=="https://test.gillions.app/gillions-sync#pairing","Testing origin never silently links to production");
        Check(PublicConnectionPresentation.PairingUrl("https://secret@example.invalid").Length==0,"invalid origin cannot expose credentials in a link");
        Check(PublicConnectionPresentation.PairingUrl("http://example.invalid").Length==0,"invalid origin cannot enable plaintext pairing");
        Check(PublicConnectionPresentation.ActionMessage("Gillions could not complete the request. Pending records were kept; please try again.",true).Contains("fresh one-time code"),"pairing failure provides a safe recovery remedy");
        Check(PublicConnectionPresentation.ActionMessage("Sync completed.")=="Sync completed.","manual sync result remains visible");
        var watch=System.Diagnostics.Stopwatch.StartNew();
        for(int i=0;i<10000;i++) state.Update(true,100,Snapshot(i),i);
        Console.WriteLine($"Public UX/Hunt Progress PASS: {checks} assertions; 10,000 synthetic model updates {watch.Elapsed.TotalMilliseconds:F2} ms (not in-game frame cost).");
    }

    private static void CheckHuntOrdering(Action<bool, string> check) {
        var state = new HuntProgressState();
        var leafkin = new HuntProgressRow("leafkin", "Blue Leafkin", 0, 3) { NpcNameId = 11 };
        var moth = new HuntProgressRow("moth", "Ja Tiika Moth", 0, 3) { NpcNameId = 22 };
        var necrosis = new HuntProgressRow("necrosis", "Necrosis", 0, 3) { NpcNameId = 33 };
        void Update(long clock, uint targetNpcNameId = 0, uint territory = 100) =>
            state.Update(true, territory, new(territory, "Yak T'el", "", false, [leafkin, moth, necrosis], clock + 6000),
                clock, targetNpcNameId);
        void Order(string why, params string[] expected) =>
            check(state.Display.Rows.Select(row => row.Key).SequenceEqual(expected), why);

        Update(0);
        Order("An initial snapshot uses bill order", "leafkin", "moth", "necrosis");
        Update(250, 33);
        Order("Targeted Necrosis moves ahead of the other bill marks", "necrosis", "leafkin", "moth");
        var targetedRows = state.Display.Rows;
        Update(500, 33);
        check(ReferenceEquals(targetedRows, state.Display.Rows), "An equivalent refreshed snapshot reuses the ordering");

        moth = moth with { Current = 1 };
        Update(750, 33);
        Order("The selected mark takes priority over a different mark's count change", "necrosis", "leafkin", "moth");
        Update(1000, 99);
        Order("A non-bill target falls back to the most recently changed count", "moth", "leafkin", "necrosis");
        var recentRows = state.Display.Rows;
        Update(1250);
        check(ReferenceEquals(recentRows, state.Display.Rows), "Losing an unrelated target does not reorder the same active mark");

        necrosis = necrosis with { Current = 1 };
        Update(1500);
        Order("A newer count change moves its mark ahead of the earlier one", "necrosis", "leafkin", "moth");
        moth = moth with { Current = 2 };
        necrosis = necrosis with { Current = 2 };
        Update(1750);
        Order("Count changes from one snapshot tie in bill order", "moth", "leafkin", "necrosis");

        moth = moth with { Current = 3 };
        Update(2000, 22);
        Order("A targeted completed mark stays below every unfinished mark", "necrosis", "leafkin", "moth");
        leafkin = leafkin with { Current = 3 };
        Update(2250, 11);
        Order("Completion holds retain their observation order", "necrosis", "moth", "leafkin");
        var finishedRows = state.Display.Rows;
        Update(4499);
        check(ReferenceEquals(finishedRows, state.Display.Rows), "Unchanged ordering is reused before either completion hold expires");
        Update(4500);
        Order("The first completed mark disappears after 2.5 seconds while hunting continues", "necrosis", "leafkin");
        Update(4749);
        Order("The later completion keeps its own hold", "necrosis", "leafkin");
        Update(4750);
        Order("The later completed mark disappears after its own 2.5 seconds", "necrosis");
        Update(5000, territory: 101);
        Order("Already completed marks do not get a new hold when entering an area", "necrosis");
        necrosis = necrosis with { Current = 0 };
        leafkin = leafkin with { Current = 0 };
        moth = moth with { Current = 0 };
        Update(5250, territory: 102);
        Order("A fresh set of marks starts in bill order", "leafkin", "moth", "necrosis");

        necrosis = necrosis with { Current = 1 };
        Update(5500, territory: 102);
        Order("New progress in the current area starts a new history", "necrosis", "leafkin", "moth");
        Update(5750, territory: 103);
        Order("A territory change clears recent progress even when the observed counts are identical", "leafkin", "moth", "necrosis");

        necrosis = necrosis with { Current = 2 };
        Update(6000, territory: 103);
        Order("Progress can promote the mark again after the territory reset", "necrosis", "leafkin", "moth");
        state.Update(true, 103, HuntProgressSnapshot.Unavailable, 6250);
        Update(6500, territory: 103);
        Order("Recovery cannot infer a recent kill from the first available count", "leafkin", "moth", "necrosis");
    }
}
