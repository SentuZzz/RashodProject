using System;

namespace WpfApp1.Helpers
{
    public static class AppMessenger
    {
        public static event Action DirectoriesUpdated;

        // НОВЫЙ СИГНАЛ: когда назначают людей в наряд
        public static event Action DutiesUpdated;

        public static void BroadcastDirectoriesUpdated() => DirectoriesUpdated?.Invoke();
        public static void BroadcastDutiesUpdated() => DutiesUpdated?.Invoke();
    }
}