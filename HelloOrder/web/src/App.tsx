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
import { hasPermission, MENU_CONFIG, PAGE_PERMISSION_MAP } from './config/pagePermissions';

function PrivateRoute({ children }: { children: React.ReactNode }) {
  const { token } = useAuth();
  if (!token) return <Navigate to="/login" replace />;
  return <>{children}</>;
}

function PermissionRoute({ path, children }: { path: string; children: React.ReactNode }) {
  const { user } = useAuth();
  const permission = PAGE_PERMISSION_MAP[path];
  if (hasPermission(user, permission)) return <>{children}</>;
  const fallback = MENU_CONFIG.find(item => hasPermission(user, item.permission))?.key || '/login';
  return <Navigate to={fallback} replace />;
}

function HomeRedirect() {
  const { user } = useAuth();
  const fallback = MENU_CONFIG.find(item => hasPermission(user, item.permission))?.key || '/login';
  return <Navigate to={fallback} replace />;
}

function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/" element={<PrivateRoute><Layout /></PrivateRoute>}>
        <Route index element={<HomeRedirect />} />
        <Route path="dashboard" element={<PermissionRoute path="/dashboard"><Dashboard /></PermissionRoute>} />
        <Route path="merchants" element={<PermissionRoute path="/merchants"><MerchantList /></PermissionRoute>} />
        <Route path="shops" element={<PermissionRoute path="/shops"><ShopList /></PermissionRoute>} />
        <Route path="products" element={<PermissionRoute path="/products"><ProductList /></PermissionRoute>} />
        <Route path="orders" element={<PermissionRoute path="/orders"><OrderList /></PermissionRoute>} />
        <Route path="orders/conversion" element={<PermissionRoute path="/orders/conversion"><OrderConversion /></PermissionRoute>} />
        <Route path="logistics" element={<PermissionRoute path="/logistics"><ExpressList /></PermissionRoute>} />
        <Route path="system/users" element={<PermissionRoute path="/system/users"><UserList /></PermissionRoute>} />
        <Route path="system/roles" element={<PermissionRoute path="/system/roles"><RoleList /></PermissionRoute>} />
        <Route path="system/permissions" element={<PermissionRoute path="/system/permissions"><PermissionList /></PermissionRoute>} />
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
