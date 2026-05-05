using System;

namespace WpfApp1.Helpers
{
    public static class AppMessenger
    {
        // Событие, которое будет срабатывать при любом изменении в справочниках
        public static event Action DirectoriesUpdated;

        // Метод, который "кричит в рупор"
        public static void BroadcastDirectoriesUpdated()
        {
            DirectoriesUpdated?.Invoke();
        }
    }
}