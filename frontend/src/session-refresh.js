// The lock is shared by all tabs of this origin. Recheck the cookie-backed
// session after acquiring it: another tab may already have refreshed it.
export function createSessionRefresh({ locks, probe, renew }) {
  let pending = null;
  return function refresh() {
    if (!pending) {
      const operation = async () => {
        try {
          return await probe();
        } catch (error) {
          if (error.status !== 401) throw error;
        }
        if (!locks) {
          const error = new Error(
            "Trình duyệt chưa hỗ trợ tự làm mới phiên an toàn. Vui lòng đăng nhập lại.",
          );
          error.status = 401;
          throw error;
        }
        return renew();
      };
      pending = Promise.resolve()
        .then(() =>
          locks
            ? locks.request("spwpos-session-refresh", operation)
            : operation(),
        )
        .finally(() => (pending = null));
    }
    return pending;
  };
}
