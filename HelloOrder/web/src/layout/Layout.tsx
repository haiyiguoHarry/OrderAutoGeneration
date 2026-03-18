import { useState } from 'react';
import { Outlet, useNavigate, useLocation } from 'react-router-dom';
import { Layout as AntLayout, Menu, Dropdown, Avatar, Space } from 'antd';
import {
  DashboardOutlined,
  ShopOutlined,
  FileTextOutlined,
  CarOutlined,
  UserOutlined,
  SettingOutlined,
  LogoutOutlined
} from '@ant-design/icons';
import { useAuth } from '../store/AuthContext';

const { Header, Sider, Content } = AntLayout;

const menuItems = [
  { key: '/dashboard', icon: <DashboardOutlined />, label: '工作台' },
  { key: '/merchants', icon: <ShopOutlined />, label: '商家管理' },
  { key: '/shops', icon: <ShopOutlined />, label: '商家店铺' },
  { key: '/products', icon: <ShopOutlined />, label: '商品管理' },
  { key: '/orders', icon: <FileTextOutlined />, label: '订单管理' },
  { key: '/orders/conversion', icon: <FileTextOutlined />, label: '订单文件转换' },
  { key: '/logistics', icon: <CarOutlined />, label: '快递与物流' },
  { key: '/system/users', icon: <SettingOutlined />, label: '用户管理' },
  { key: '/system/roles', icon: <SettingOutlined />, label: '角色管理' },
  { key: '/system/permissions', icon: <SettingOutlined />, label: '页面权限管理' }
];

export default function Layout() {
  const [collapsed, setCollapsed] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();
  const { user, logout } = useAuth();

  const userMenu = {
    items: [
      { key: 'profile', icon: <UserOutlined />, label: '个人中心' },
      { type: 'divider' as const },
      { key: 'logout', icon: <LogoutOutlined />, label: '退出登录', onClick: () => { logout(); navigate('/login'); } }
    ]
  };

  return (
    <AntLayout style={{ minHeight: '100%' }}>
      <Sider collapsible collapsed={collapsed} onCollapse={setCollapsed}>
        <div style={{ height: 32, margin: 16, color: '#fff', fontSize: 18, textAlign: 'center' }}>
          HelloOrder
        </div>
        <Menu
          theme="dark"
          selectedKeys={[location?.pathname ?? '/']}
          mode="inline"
          items={menuItems}
          onClick={({ key }) => navigate(key)}
        />
      </Sider>
      <AntLayout>
        <Header style={{ padding: '0 24px', background: '#fff', display: 'flex', justifyContent: 'flex-end', alignItems: 'center' }}>
          <Dropdown menu={userMenu} placement="bottomRight">
            <Space style={{ cursor: 'pointer' }}>
              <Avatar icon={<UserOutlined />} />
              <span>{user?.realName || user?.username}</span>
            </Space>
          </Dropdown>
        </Header>
        <Content style={{ margin: '24px', padding: 24, background: '#fff', minHeight: 280 }}>
          <Outlet />
        </Content>
      </AntLayout>
    </AntLayout>
  );
}
