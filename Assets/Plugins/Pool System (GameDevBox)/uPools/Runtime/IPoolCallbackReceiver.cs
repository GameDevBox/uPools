namespace uPools
{
    public interface IPoolCallbackReceiver
    {
        void OnRent();

        void OnReturn();

        void OnInitialize();

        void OnPoolDestroy();
    }
}