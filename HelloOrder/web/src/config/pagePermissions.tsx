import type { UserInfo } from '../store/AuthContext';
import {
  DashboardOutlined,
  ShopOutlined,
  FileTextOutlined,
  CarOutlined,
  SettingOutlined
} from '@ant-design/icons';
import type { ReactNode } from 'react';

export interface MenuConfigItem {
  key: string;
  label: string;
  icon: ReactNode;
  permission: string;
}

export const MENU_CONFIG: MenuConfigItem[] = [
  { key: '/dashboard', icon: <DashboardOutlined />, label: '工作台', permission: 'menu:dashboard' },
  { key: '/merchants', icon: <ShopOutlined />, label: '商家管理', permission: 'menu:merchants' },
  { key: '/shops', icon: <ShopOutlined />, label: '商家店铺', permission: 'menu:shops' },
  { key: '/products', icon: <ShopOutlined />, label: '商品管理', permission: 'menu:products' },
  { key: '/orders', icon: <FileTextOutlined />, label: '订单管理', permission: 'menu:orders' },
  { key: '/orders/conversion', icon: <FileTextOutlined />, label: '订单文件转换', permission: 'menu:orders:conversion' },
  { key: '/logistics', icon: <CarOutlined />, label: '快递与物流', permission: 'menu:logistics' },
  { key: '/system/users', icon: <SettingOutlined />, label: '用户管理', permission: 'menu:system:users' },
  { key: '/system/roles', icon: <SettingOutlined />, label: '角色管理', permission: 'menu:system:roles' },
  { key: '/system/permissions', icon: <SettingOutlined />, label: '页面权限管理', permission: 'menu:system:permissions' }
];

export const PAGE_PERMISSION_MAP: Record<string, string> = {
  '/dashboard': 'menu:dashboard',
  '/merchants': 'menu:merchants',
  '/shops': 'menu:shops',
  '/products': 'menu:products',
  '/orders': 'menu:orders',
  '/orders/conversion': 'menu:orders:conversion',
  '/logistics': 'menu:logistics',
  '/system/users': 'menu:system:users',
  '/system/roles': 'menu:system:roles',
  '/system/permissions': 'menu:system:permissions'
};

export function hasPermission(user: UserInfo | null, permission?: string) {
  if (!permission) return true;
  if (!user) return false;
  if ((user.roleCode || '').toLowerCase() === 'admin') return true;
  return user.permissions.includes(permission);
}
