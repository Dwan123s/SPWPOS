export const adminRoutes = {
  "admin-users": [
    "Quản lý người dùng",
    "Tìm kiếm, kiểm soát tài khoản và lịch sử khóa/mở khóa.",
    "users",
  ],
  "admin-revenue": [
    "Doanh thu & đơn hàng",
    "Theo dõi doanh thu, thanh toán và lịch sử mua hàng.",
    "insights",
  ],
  "admin-system": [
    "Trạng thái hệ thống",
    "Theo dõi vận hành, dịch vụ và lịch sử xử lý hệ thống.",
    "shield",
  ],
};
export const isAdminRoute = (route) =>
  route === "admin" || Object.hasOwn(adminRoutes, route);
