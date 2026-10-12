using System.IO;
using System.IO.MemoryMappedFiles;
using XWhat.Core;

namespace XWhat.App.Services;

internal sealed class RtssOutput : IDisposable
{
    private readonly string _owner = $"XWhat.{Environment.ProcessId}";
    public string Status { get; private set; } = "Запустите RTSS для полноэкранного вывода";

    public void Update(string text, RtssGraph[] graphs) => Access(memory =>
    {
        RtssProtocol.TryWrite(memory, _owner, text, graphs, out var error);
        Status = error;
    });
    public void Clear() => Access(memory => RtssProtocol.Clear(memory, _owner));
    private delegate void MemoryAction(Span<byte> memory);
    private unsafe void Access(MemoryAction action)
    {
        try
        {
            using var file = MemoryMappedFile.OpenExisting("RTSSSharedMemoryV2", MemoryMappedFileRights.ReadWrite);
            using var view = file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
            if (view.Capacity is < 40 or > 64 * 1024 * 1024) { Status = "RTSS: неверный размер памяти"; return; }
            byte* pointer = null;
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
            try
            {
                pointer += view.PointerOffset;
                var memory = new Span<byte>(pointer, (int)view.Capacity);
                if (System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(memory) != 0x52545353 ||
                    System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(memory[4..]) < 0x0002000e)
                { Status = "Нужен RTSS с форматом памяти 2.14+"; return; }
                ref var busy = ref *(int*)(pointer + 36);
                var previous = Volatile.Read(ref busy);
                if ((previous & 1) != 0 || Interlocked.CompareExchange(ref busy, previous | 1, previous) != previous) return;
                try { action(memory); }
                finally { Interlocked.And(ref busy, ~1); }
            }
            finally { view.SafeMemoryMappedViewHandle.ReleasePointer(); }
        }
        catch (FileNotFoundException) { Status = "RTSS не запущен · установите и запустите RTSS"; }
        catch (UnauthorizedAccessException) { Status = "Нет доступа к RTSS · проверьте одинаковые права запуска"; }
        catch (IOException) { Status = "RTSS перезапускается"; }
    }
    public void Dispose() => Clear();
}
