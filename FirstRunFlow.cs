using System;
namespace QuickTools;
internal enum WelcomeStep { Welcome,Components,Installing,Trigger,Batch,Scripts,Style }
internal sealed class FirstRunFlow
{
    internal WelcomeStep Step { get; private set; }
    internal bool Completed { get; private set; }
    internal FirstRunFlow(bool replay=false) { Step=replay?WelcomeStep.Trigger:WelcomeStep.Welcome; }
    internal void Next()
    {
        if(Step==WelcomeStep.Installing) throw new InvalidOperationException("Сначала завершите установку или явно пропустите компоненты.");
        if(Step==WelcomeStep.Style) { Completed=true; return; }
        Step++;
    }
    internal void Prepared() { if(Step!=WelcomeStep.Installing) throw new InvalidOperationException("Установка ещё не начата"); Step=WelcomeStep.Trigger; }
    internal void Retry() { if(Step!=WelcomeStep.Installing) throw new InvalidOperationException("Нет прерванной установки"); Step=WelcomeStep.Components; }
    internal void Back() { if(Step is WelcomeStep.Installing or WelcomeStep.Trigger or WelcomeStep.Welcome) return; Step--; }
}
