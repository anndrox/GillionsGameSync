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
        state.Update(true,101,Snapshot(7100,3,false,101),7100); Check(state.Display.Status.Contains("updating"),"unknown coverage cannot claim all-complete");
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
}
