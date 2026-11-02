using System.Collections.Generic;
namespace SkyCoop;
public sealed class AttemptBudget
{
 private readonly Dictionary<long,(int progress,double next)> attempts=new();
 public bool Allow(long id,int progress,double now)
 {
  if(attempts.TryGetValue(id,out var old)&&old.progress==progress&&now<old.next)return false;
  attempts[id]=(progress,now+.5);return true;
 }
 public void Clear()=>attempts.Clear();
}
