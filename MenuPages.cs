using System;
using System.Linq;
namespace QuickTools;
internal static class MenuPages
{
    internal const int Capacity=7;
    internal static int Count(ToolAction[] actions)=>actions.Length<=9?1:(actions.Length+Capacity-1)/Capacity;
    internal static ToolAction[] Get(ToolAction[] actions,int page)
    {
        int count=Count(actions); if(count==1) return actions;
        page=Math.Clamp(page,0,count-1);
        return actions.Skip(page*Capacity).Take(Capacity).Concat(new[]{new ToolAction("Назад","","page",new[]{((page+count-1)%count).ToString()}),new ToolAction("Ещё →","","page",new[]{((page+1)%count).ToString()})}).ToArray();
    }
}
