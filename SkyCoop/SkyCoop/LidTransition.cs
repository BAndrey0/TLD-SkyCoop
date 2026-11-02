namespace SkyCoop;
public sealed class LidTransition
{
 public string Current{get;private set;}="close";
 public string Queued{get;private set;}
 public bool Start(string requested,bool animating)
 {
  if(requested==Current){Queued=null;return false;}
  if(animating){Queued=requested;return false;}
  Current=requested;return true;
 }
 public string Complete(){string next=Queued;Queued=null;return next;}
}
