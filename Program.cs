/****************/
// Mã sinh viên: 202418929
// Họ tên: Hoàng Chí Kiệt
/****************/

using System;
using System.Collections.Generic;
using System.Linq;

namespace Lab07_GenericAndPolymorphism
{
    // ================== 1. ENUMS VÀ INTERFACES CƠ SỞ ==================
    public enum DeviceStatus { Available, Borrowed, UnderMaintenance }

    public interface IIdentifiable
    {
        string Id { get; }
    }

    // ================== 2. LỚP THIẾT BỊ (ĐA HÌNH) ==================
    public abstract class TeachingDevice : IIdentifiable
    {
        public string Id { get; }
        public string Name { get; }
        public string StorageLocation { get; }
        public DeviceStatus Status { get; protected set; }

        protected TeachingDevice(string id, string name, string storageLocation)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Mã thiết bị không hợp lệ.");
            Id = id;
            Name = name;
            StorageLocation = storageLocation;
            Status = DeviceStatus.Available;
        }

        public abstract bool IsReadyForUse();
        public abstract double GetDefaultPriorityScore();

        public void Borrow()
        {
            if (!IsReadyForUse()) throw new DeviceNotReadyException(Id, Status);
            Status = DeviceStatus.Borrowed;
        }

        public void Return()
        {
            if (Status != DeviceStatus.Borrowed) throw new InvalidOperationException("Thiết bị này chưa được mượn.");
            Status = DeviceStatus.Available;
        }
        
        public override string ToString() => $"[{Id}] {Name} (Trạng thái: {Status})";
    }

    public class ProjectorRemote : TeachingDevice
    {
        public int BatteryLevel { get; }
        public ProjectorRemote(string id, string name, string loc, int battery) : base(id, name, loc) { BatteryLevel = battery; }
        
        // Sẵn sàng khi pin >= 20%
        public override bool IsReadyForUse() => Status == DeviceStatus.Available && BatteryLevel >= 20;
        public override double GetDefaultPriorityScore() => BatteryLevel;
    }

    public class AirConditionerRemote : TeachingDevice
    {
        public IReadOnlySet<string> CompatibleRooms { get; }
        
        public AirConditionerRemote(string id, string name, string loc, HashSet<string> rooms) : base(id, name, loc) 
        { CompatibleRooms = rooms; }

        public bool IsCompatibleWith(string roomId) => CompatibleRooms.Contains(roomId);
        public override bool IsReadyForUse() => Status == DeviceStatus.Available;
        public override double GetDefaultPriorityScore() => 100;
    }

    public class WirelessMicrophone : TeachingDevice
    {
        public int BatteryLevel { get; }
        public int SignalQuality { get; }
        
        public WirelessMicrophone(string id, string name, string loc, int battery, int signal) : base(id, name, loc)
        { BatteryLevel = battery; SignalQuality = signal; }

        // Sẵn sàng khi pin >= 30 và tín hiệu >= 40
        public override bool IsReadyForUse() => Status == DeviceStatus.Available && BatteryLevel >= 30 && SignalQuality >= 40;
        public override double GetDefaultPriorityScore() => (BatteryLevel + SignalQuality) / 2.0;
    }

    // ================== 3. KHO LƯU TRỮ (GENERIC) ==================
    public interface IRepository<T> where T : IIdentifiable
    {
        void Add(T item);
        bool Remove(string id);
        T? FindById(string id);
        IReadOnlyList<T> GetAll();
        IReadOnlyList<T> FindAll(Predicate<T> condition);
    }

    public class InMemoryRepository<T> : IRepository<T> where T : IIdentifiable
    {
        private readonly Dictionary<string, T> _items = new();

        public void Add(T item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (_items.ContainsKey(item.Id)) throw new DuplicateIdException(item.Id);
            _items[item.Id] = item;
        }

        public bool Remove(string id) => _items.Remove(id);
        public T? FindById(string id) => _items.TryGetValue(id, out var item) ? item : default;
        public IReadOnlyList<T> GetAll() => _items.Values.ToList().AsReadOnly();
        public IReadOnlyList<T> FindAll(Predicate<T> condition) => _items.Values.Where(i => condition(i)).ToList().AsReadOnly();
    }

    // ================== 4. CHIẾN LƯỢC LỰA CHỌN (STRATEGY) ==================
    public interface ISelectionStrategy<T>
    {
        T Select(IReadOnlyList<T> candidates);
    }

    public class HighestPriorityStrategy<T> : ISelectionStrategy<T>
    {
        private readonly Func<T, double> _scoreSelector;
        public HighestPriorityStrategy(Func<T, double> scoreSelector) { _scoreSelector = scoreSelector; }

        public T Select(IReadOnlyList<T> candidates)
        {
            if (candidates == null || candidates.Count == 0) throw new NoSuitableItemException();
            return candidates.OrderByDescending(_scoreSelector).First();
        }
    }

    public class FirstAvailableStrategy<T> : ISelectionStrategy<T>
    {
        public T Select(IReadOnlyList<T> candidates)
        {
            if (candidates == null || candidates.Count == 0) throw new NoSuitableItemException();
            return candidates.First(); // Trả về phần tử đầu tiên
        }
    }

    // ================== 5. DỊCH VỤ CẤP PHÁT TỔNG QUÁT ==================
    public class AllocationService<T> where T : TeachingDevice
    {
        private readonly IRepository<T> _repository;
        private ISelectionStrategy<T> _strategy;

        public AllocationService(IRepository<T> repository, ISelectionStrategy<T> strategy)
        {
            _repository = repository;
            _strategy = strategy;
        }

        public void ChangeStrategy(ISelectionStrategy<T> newStrategy)
        {
            _strategy = newStrategy;
        }

        public T Allocate(Predicate<T> additionalCondition)
        {
            // Lọc ra các ứng viên IsReadyForUse == true VÀ thỏa mãn điều kiện truyền vào
            var candidates = _repository.FindAll(d => d.IsReadyForUse() && (additionalCondition == null || additionalCondition(d)));
            
            T selected = _strategy.Select(candidates);
            selected.Borrow();
            return selected;
        }
    }

    // ================== 6. NGOẠI LỆ NGHIỆP VỤ ==================
    public class DuplicateIdException : Exception { public DuplicateIdException(string id) : base($"Mã {id} đã tồn tại trong kho.") { } }
    public class NoSuitableItemException : Exception { public NoSuitableItemException() : base("Không tìm thấy thiết bị nào phù hợp với yêu cầu.") { } }
    public class DeviceNotReadyException : Exception { public DeviceNotReadyException(string id, DeviceStatus status) : base($"Thiết bị {id} không sẵn sàng (Status: {status}).") { } }

    // ================== 7. KIỂM THỬ 11 BƯỚC ==================
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Khởi tạo các kho (Repository Generic dùng chung)
            var projRepo = new InMemoryRepository<ProjectorRemote>();
            var acRepo = new InMemoryRepository<AirConditionerRemote>();
            var micRepo = new InMemoryRepository<WirelessMicrophone>();

            Console.WriteLine("--- BẮT ĐẦU KIỂM THỬ 11 YÊU CẦU ---");

            // 1. Thêm thiết bị vào repository
            projRepo.Add(new ProjectorRemote("PRJ_01", "ĐK Máy chiếu P1", "Tủ A", 85));
            projRepo.Add(new ProjectorRemote("PRJ_02", "ĐK Máy chiếu P2", "Tủ A", 15)); // Pin quá yếu, không thể mượn
            projRepo.Add(new ProjectorRemote("PRJ_03", "ĐK Máy chiếu P3", "Tủ A", 95));
            
            acRepo.Add(new AirConditionerRemote("AC_01", "ĐK Điều hòa tầng 1", "Tủ B", new HashSet<string> { "D9_201", "D9_202" }));
            acRepo.Add(new AirConditionerRemote("AC_02", "ĐK Điều hòa tầng 2", "Tủ B", new HashSet<string> { "D9_301" }));

            micRepo.Add(new WirelessMicrophone("MIC_01", "Micro Cầm Tay 1", "Tủ C", 50, 50));
            micRepo.Add(new WirelessMicrophone("MIC_02", "Micro Cầm Tay 2", "Tủ C", 90, 95)); // Tốt nhất
            micRepo.Add(new WirelessMicrophone("MIC_03", "Micro Cầm Tay 3", "Tủ C", 10, 20)); // Pin và sóng hẻo
            Console.WriteLine("1. Đã thêm đầy đủ thiết bị vào các kho.");

            // 2. Thử thêm thiết bị trùng mã
            try { projRepo.Add(new ProjectorRemote("PRJ_01", "ĐK Lỗi", "Tủ A", 100)); }
            catch (DuplicateIdException ex) { Console.WriteLine($"2. Bắt lỗi trùng mã thành công: {ex.Message}"); }

            // 3. Tìm thiết bị theo mã
            var foundMic = micRepo.FindById("MIC_02");
            Console.WriteLine($"3. Tìm thiết bị theo mã 'MIC_02': {foundMic.Name}");

            // 4. Lọc các thiết bị sẵn sàng
            var readyProjs = projRepo.FindAll(d => d.IsReadyForUse());
            Console.WriteLine($"4. Lọc ĐK Máy chiếu sẵn sàng: Tìm thấy {readyProjs.Count} cái (Đã loại trừ cái pin yếu).");

            // 5. Chọn điều khiển máy chiếu có điểm cao nhất
            var projService = new AllocationService<ProjectorRemote>(
                projRepo, 
                new HighestPriorityStrategy<ProjectorRemote>(d => d.GetDefaultPriorityScore())
            );
            var bestProj = projService.Allocate(d => true);
            Console.WriteLine($"5. Chọn máy chiếu điểm cao nhất: {bestProj.Name} (Pin: {bestProj.BatteryLevel}%)");

            // 6. Chọn điều khiển điều hòa tương thích với một phòng
            var acService = new AllocationService<AirConditionerRemote>(
                acRepo, 
                new FirstAvailableStrategy<AirConditionerRemote>()
            );
            var roomAc = acService.Allocate(d => d.IsCompatibleWith("D9_202"));
            Console.WriteLine($"6. Mượn điều hòa cho phòng D9_202: {roomAc.Name}");

            // 7. Chọn micro đầu tiên sẵn sàng
            var micService = new AllocationService<WirelessMicrophone>(
                micRepo, 
                new FirstAvailableStrategy<WirelessMicrophone>()
            );
            var firstMic = micService.Allocate(d => true);
            Console.WriteLine($"7. Chọn Micro đầu tiên sẵn sàng (Chiến lược FirstAvailable): {firstMic.Name}");

            // 8. Đổi chiến lược trong thời gian chạy và quan sát kết quả
            micService.ChangeStrategy(new HighestPriorityStrategy<WirelessMicrophone>(d => d.GetDefaultPriorityScore()));
            var bestMic = micService.Allocate(d => true);
            Console.WriteLine($"8. Đổi sang chiến lược HighestPriority, mượn tiếp được: {bestMic.Name}");

            // 9. Thử cấp phát khi không có ứng viên phù hợp
            try { acService.Allocate(d => d.IsCompatibleWith("PHONG_VIP_999")); }
            catch (NoSuitableItemException ex) { Console.WriteLine($"9. Không có ứng viên phù hợp: Bắt lỗi thành công ({ex.Message})"); }

            // 10. Chứng minh thiết bị được chọn chuyển sang Borrowed
            Console.WriteLine($"10. Trạng thái máy chiếu {bestProj.Id} sau khi cấp phát là: {bestProj.Status}");

            // 11. Chứng minh repository tổng quát dùng cho ít nhất 2 loại thiết bị mà không lặp code
            Console.WriteLine($"11. Chứng minh Generic: Kho máy chiếu chứa {projRepo.GetAll().Count} phần tử, Kho Điều hòa chứa {acRepo.GetAll().Count} phần tử. Cả 2 đều dùng chung code của InMemoryRepository<T>.");
            
            // Trả thiết bị
            bestProj.Return();
            Console.WriteLine($"--> Bonus: Trả máy chiếu về kho thành công, trạng thái: {bestProj.Status}");
        }
    }
}