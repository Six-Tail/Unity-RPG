using System;
using System.Collections.Generic;
using UnityEngine;
#if IAP_PRESENT
using System.Linq;
using Unity.Services.Core;
using UnityEngine.Purchasing;
#endif

namespace SubcultureRPG.Meta.Shop
{
    /// <summary>
    /// 인앱 결제(Unity IAP 5 — PurchasingController · 구글 플레이 · 앱 스토어).
    ///
    /// · 안드로이드 · iOS: 실제 결제. 에디터: Unity IAP 가짜 스토어(돈이 나가지 않는 테스트 결제 창).
    /// · Windows(직접 배포): 스토어가 없어 결제 불가 — 모바일에서 산 것은 같은 계정이면 클라우드 저장으로 PC에도 반영.
    /// · Unity IAP 패키지가 없을 때(IAP_PRESENT 꺼짐): 에디터 · 개발 빌드에서만 '테스트 결제'(바로 지급), 출시 빌드는 결제 불가.
    ///
    /// 지급: 결제가 끝나면 IapCatalog 의 grant 로 지급하고 거래 ID 를 기록(같은 거래를 두 번 주지 않음).
    /// 게임이 아직 준비되지 않았으면(지갑 등이 없음) 거래를 '보류'로 두고 준비되면 지급 후 확정 — 앱이 꺼져도 다음 실행 때 다시 옴.
    /// 영수증 서버 검증은 아직 없음(docs/iap.md 참고).
    /// </summary>
    public class IapStore : MonoBehaviour
    {
        public enum StoreMode { Connecting, Store, TestGrant, Unavailable }

        private const string TxKey = "iap.tx";   // 지급을 마친 거래 ID(최근 200개)

        private static IapStore _instance;
        public static IapStore Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<IapStore>();
                if (_instance == null)
                {
                    var go = new GameObject("IapStore");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<IapStore>();
                }
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { _ = Instance; }

        public StoreMode Mode { get; private set; } = StoreMode.Connecting;
        public string UnavailableReason { get; private set; } = "";

        /// <summary>상품이 지급됐을 때(화면 갱신 · 알림용).</summary>
        public event Action<IapProduct> OnGranted;

        private Action<string> _waiting;     // 진행 중인 구매의 결과(null = 성공, 아니면 오류 문구)
        private string _waitingId;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
#if IAP_PRESENT
  #if UNITY_STANDALONE && !UNITY_EDITOR
            SetUnavailable("PC 버전에서는 결제할 수 없습니다.\n모바일에서 같은 계정으로 구매하면 PC에도 반영됩니다.");
  #else
            InitStore();
  #endif
#else
            if (SubcultureRPG.UI.DevTools.Enabled) Mode = StoreMode.TestGrant;
            else SetUnavailable("결제 기능을 준비하고 있습니다.");
#endif
        }

        private void SetUnavailable(string why) { Mode = StoreMode.Unavailable; UnavailableReason = why; }

        // ---------------- 화면에서 쓰는 것 ----------------

        /// <summary>표시 가격: 스토어가 알려 준 현지 가격(예: ₩5,900) → 못 받았으면 원화 기본 가격.</summary>
        public string PriceOf(IapProduct p)
        {
            if (p == null) return "";
#if IAP_PRESENT && !UNITY_EDITOR
            // (에디터 가짜 스토어는 $0.01 같은 가짜 가격을 주므로 원화 기본 가격을 보여 줌)
            var sp = _pc != null ? _pc.GetProductById(p.id, _store) : null;
            if (sp != null && sp.availableToPurchase && sp.metadata != null && !string.IsNullOrEmpty(sp.metadata.localizedPriceString)) return sp.metadata.localizedPriceString;
#endif
            return p.KrwText;
        }

        public bool Busy => _waiting != null;

        /// <summary>
        /// 구매 시작. done(null) = 지급 완료, done(문구) = 실패 · 취소 이유.
        /// 살 수 없는 상태(한도 · 기간 · 이미 보유)는 결제 창을 띄우기 전에 막음.
        /// </summary>
        public void Buy(IapProduct p, Action<string> done)
        {
            if (p == null) { done?.Invoke("상품을 찾을 수 없습니다"); return; }
            string block = p.block?.Invoke();
            if (block != null) { done?.Invoke(block); return; }
            if (_waiting != null) { done?.Invoke("다른 결제가 진행 중입니다"); return; }
            switch (Mode)
            {
                case StoreMode.Connecting: done?.Invoke("스토어에 연결하는 중입니다. 잠시 후 다시 시도하세요"); return;
                case StoreMode.Unavailable: done?.Invoke(UnavailableReason); return;
                case StoreMode.TestGrant:
                    // 패키지가 없는 개발 환경: 결제 없이 바로 지급(출시 빌드에서는 이 모드가 없음)
                    if (!p.grant()) { done?.Invoke("지금은 지급할 수 없습니다(게임 준비 중)"); return; }
                    Granted(p);
                    done?.Invoke(null);
                    return;
            }
#if IAP_PRESENT
            var sp = _pc != null ? _pc.GetProductById(p.id, _store) : null;
            if (sp == null || !sp.availableToPurchase) { done?.Invoke("스토어에 등록되지 않은 상품입니다"); return; }
            _waiting = done; _waitingId = p.id;
            _pc.PurchaseProduct(sp);
#else
            done?.Invoke("결제를 사용할 수 없습니다");
#endif
        }

        private void Granted(IapProduct p)
        {
            MissionEvents.Report(MissionGoal.ShopBuy);
            OnGranted?.Invoke(p);
        }

        private void Finish(string productId, string error)
        {
            if (_waiting == null || (productId != null && _waitingId != productId)) return;
            var cb = _waiting;
            _waiting = null; _waitingId = null;
            cb(error);
        }

        // ---------------- 거래 기록(두 번 지급 방지) ----------------

        private static List<string> LoadTx()
        {
            string s = SaveStore.GetString(TxKey, "");
            return string.IsNullOrEmpty(s) ? new List<string>() : new List<string>(s.Split('|'));
        }

        private static bool AlreadyGranted(string tx) => !string.IsNullOrEmpty(tx) && LoadTx().Contains(tx);

        private static void RecordTx(string tx)
        {
            if (string.IsNullOrEmpty(tx)) return;
            var l = LoadTx();
            l.Add(tx);
            if (l.Count > 200) l.RemoveRange(0, l.Count - 200);
            SaveStore.SetString(TxKey, string.Join("|", l));
            SaveStore.Save();
        }

#if IAP_PRESENT
        // ---------------- Unity IAP 5 ----------------

        private PurchasingController _pc;
        private string _store;
        private readonly List<PendingOrder> _pending = new();   // 결제는 됐지만 게임 준비 전이라 아직 지급하지 않은 주문
        private float _retry;

        private async void InitStore()
        {
            // Unity Gaming Services 먼저(클라우드 저장이 이미 켰으면 그대로)
            try { if (UnityServices.State == ServicesInitializationState.Uninitialized) await UnityServices.InitializeAsync(); }
            catch (Exception e) { Debug.LogWarning("[IAP] Unity Services 초기화 실패: " + e.Message); }
            try
            {
                _store = UnityIAPServices.GetDefaultStore();   // 안드로이드 = 구글 플레이, iOS = 앱 스토어, 에디터 = 가짜 스토어
                _pc = new PurchasingController(_store);
                _pc.OnStoreConnected += OnStoreConnected;
                _pc.OnStoreDisconnected += OnStoreDisconnected;
                _pc.OnProductsFetched += OnProductsFetched;
                _pc.OnProductsFetchFailed += OnProductsFetchFailed;
                _pc.OnPurchasePending += OnPurchasePending;
                _pc.OnPurchaseConfirmed += OnPurchaseConfirmed;
                _pc.OnPurchaseFailed += OnPurchaseFailed;
                _pc.OnPurchaseDeferred += OnPurchaseDeferred;
                _pc.OnPurchasesFetched += OnPurchasesFetched;
                _pc.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
                await _pc.Connect();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[IAP] 스토어 연결 실패: " + e.Message);
                SetUnavailable("스토어에 연결하지 못했습니다. 네트워크를 확인한 뒤 다시 실행하세요");
            }
        }

        private void OnStoreConnected(string storeName)
        {
            var defs = IapCatalog.All.Select(p => new ProductDefinition(p.id, ProductType.Consumable)).ToList();
            _pc.FetchProducts(defs, _store);
        }

        private void OnStoreDisconnected(string storeName, StoreConnectionFailureDescription d)
        {
            Debug.LogWarning($"[IAP] 스토어 연결 끊김: {d?.message}");
            if (Mode != StoreMode.Store) SetUnavailable("스토어에 연결하지 못했습니다. 네트워크를 확인한 뒤 다시 실행하세요");
        }

        private void OnProductsFetched(string storeName, List<Product> products)
        {
            Mode = StoreMode.Store;
            Debug.Log($"[IAP] 스토어 연결 · 상품 {products.Count}개");
            _pc.FetchPurchases(_store);   // 지난번에 결제만 되고 지급 · 확정 전에 끝난 주문이 있으면 다시 OnPurchasePending 으로 옴
        }

        private void OnProductsFetchFailed(string storeName, ProductFetchFailed f)
        {
            Debug.LogWarning($"[IAP] 상품 {f.FailedFetchProducts.Count}개를 불러오지 못함: {f.FailureReason}");
            if (Mode == StoreMode.Connecting) SetUnavailable("상품 정보를 불러오지 못했습니다(스토어에 상품 등록 확인)");
        }

        private void OnPurchasesFetched(string storeName, Orders orders) { }

        private void OnPurchasesFetchFailed(string storeName, PurchasesFetchFailureDescription d) =>
            Debug.LogWarning($"[IAP] 구매 내역을 불러오지 못함: {d?.message}");

        private void OnPurchasePending(string storeName, PendingOrder order)
        {
            if (TryGrant(order)) _pc.ConfirmPurchase(order);
            else if (!_pending.Contains(order)) _pending.Add(order);   // 준비되면 Update 에서 지급 후 확정
        }

        private void OnPurchaseConfirmed(string storeName, Order order)
        {
            if (order is FailedOrder f) Debug.LogWarning($"[IAP] 확정 실패 {ProductIdOf(f)}: {f.FailureReason} {f.Details}");
        }

        private void OnPurchaseFailed(string storeName, FailedOrder order) =>
            Finish(ProductIdOf(order), FailText(order.FailureReason));

        private void OnPurchaseDeferred(string storeName, DeferredOrder order) =>
            Finish(ProductIdOf(order), "결제가 승인 대기 중입니다(승인되면 자동으로 지급됩니다)");

        private void Update()
        {
            if (_pending.Count == 0) return;
            _retry -= Time.unscaledDeltaTime;
            if (_retry > 0f) return;
            _retry = 2f;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var o = _pending[i];
                if (!TryGrant(o)) continue;
                _pending.RemoveAt(i);
                _pc?.ConfirmPurchase(o);
            }
        }

        private static string ProductIdOf(Order o)
        {
            var items = o?.CartOrdered?.Items();
            return items != null && items.Count > 0 ? items[0].Product?.definition?.id : null;
        }

        /// <summary>지급(이미 준 거래면 지급 없이 true). 게임이 준비되지 않았으면 false.</summary>
        private bool TryGrant(Order o)
        {
            string id = ProductIdOf(o), tx = o?.Info?.TransactionID;
            if (AlreadyGranted(tx)) { Finish(id, null); return true; }
            var p = IapCatalog.Find(id);
            if (p == null) { Debug.LogWarning($"[IAP] 모르는 상품 {id} — 지급 없이 확정"); Finish(id, "알 수 없는 상품입니다"); return true; }
            if (!p.grant()) return false;
            RecordTx(tx);
            Granted(p);
            Finish(id, null);
            return true;
        }

        private static string FailText(PurchaseFailureReason r) => r switch
        {
            PurchaseFailureReason.UserCancelled => "결제를 취소했습니다",
            PurchaseFailureReason.OrderCancelled => "결제를 취소했습니다",
            PurchaseFailureReason.PaymentDeclined => "결제가 거절되었습니다",
            PurchaseFailureReason.PurchasingUnavailable => "이 기기에서 결제를 사용할 수 없습니다",
            PurchaseFailureReason.StoreNotConnected => "스토어에 연결되어 있지 않습니다",
            PurchaseFailureReason.ProductUnavailable => "지금 살 수 없는 상품입니다",
            PurchaseFailureReason.ExistingPurchasePending => "이전 결제가 아직 처리 중입니다. 잠시 후 다시 시도하세요",
            PurchaseFailureReason.UserNotAuthenticated => "스토어 계정에 로그인되어 있지 않습니다",
            PurchaseFailureReason.DuplicateTransaction => "이미 처리된 결제입니다",
            _ => "결제하지 못했습니다",
        };
#endif
    }
}
