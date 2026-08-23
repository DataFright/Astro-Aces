namespace UTI
{
    /// <summary>A destination BeanLogger can write captured samples to.</summary>
    public interface IBeanOutput
    {
        void Open(BeanTracker tracker);
        void Write(BeanSample sample);
        void Close();
    }
}
