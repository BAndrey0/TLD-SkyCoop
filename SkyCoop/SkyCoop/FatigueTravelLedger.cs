namespace SkyCoop;

// Session memory, independent of scene-local/native save snapshots.
public sealed class FatigueTravelLedger
{
	private float? value;
	private string scene;
	private long owner;
	private bool pending;
	public float? Last => value;
	public void Reset() { value=null;scene=null;owner=0;pending=false; }
	public void Loading() { pending=true; }
	public float? Enter(string nextScene, long nextOwner)
	{
		float? restore=pending||scene!=nextScene||owner!=nextOwner?value:null;
		scene=nextScene;owner=nextOwner;pending=false;return restore;
	}
	public void Remember(float current) { if(float.IsFinite(current)&&current>=0)value=current; }
}
