import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './store/AuthContext';
import Layout from './layout/Layout';
import Login from './pages/Login';
import Dashboard from './pages/Dashboard';
import MerchantList from './pages/merchant/MerchantList';
import ShopList from './pages/merchant/ShopList';
import ProductList from './pages/merchant/ProductList';
import OrderList from './pages/order/OrderList';
import OrderConversion from './pages/order/OrderConversion';
import ExpressList from './pages/logistics/ExpressList';
import UserList from './pages/system/UserList';
import RoleList from './pages/system/RoleList';
import PermissionList from './pages/system/PermissionList';

function PrivateRoute({ children }: { children: React.ReactNode }) {
  const { token } = useAuth();
  if (!token) return <Navigate to="/login" replace />;
  return <>{children}</>;
}

function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/" element={<PrivateRoute><Layout /></PrivateRoute>}>
        <Route index element={<Navigate to="/dashboard" replace />} />
        <Route path="dashboard" element={<Dashboard />} />
        <Route path="merchants" element={<MerchantList />} />
        <Route path="shops" element={<ShopList />} />
        <Route path="products" element={<ProductList />} />
        <Route path="orders" element={<OrderList />} />
        <Route path="orders/conversion" element={<OrderConversion />} />
        <Route path="logistics" element={<ExpressList />} />
        <Route path="system/users" element={<UserList />} />
        <Route path="system/roles" element={<RoleList />} />
        <Route path="system/permissions" element={<PermissionList />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <AppRoutes />
      </AuthProvider>
    </BrowserRouter>
  );
}
