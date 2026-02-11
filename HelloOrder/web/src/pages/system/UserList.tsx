import { useState, useEffect } from 'react';
import { Table, Tag, Button, Space, Input, Select, message, Modal, Form, Popconfirm } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { api } from '../../services/api';

interface UserRow {
  id: string;
  username: string;
  realName: string;
  roleId?: string;
  roleName?: string;
  status: number;
  createdAt: string;
  updatedAt?: string;
}

interface RoleOption {
  id: string;
  code: string;
  name: string;
}

const STATUS_OPTIONS = [
  { value: 1, label: '正常' },
  { value: 0, label: '禁用' }
];

export default function UserList() {
  const [list, setList] = useState<UserRow[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [loading, setLoading] = useState(false);
  const [roles, setRoles] = useState<RoleOption[]>([]);
  const [username, setUsername] = useState('');
  const [realName, setRealName] = useState('');
  const [roleIdFilter, setRoleIdFilter] = useState<string | null>(null);
  const [statusFilter, setStatusFilter] = useState<number | null>(null);

  const [addModalOpen, setAddModalOpen] = useState(false);
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form] = Form.useForm();
  const [editForm] = Form.useForm();

  const load = async () => {
    setLoading(true);
    try {
      let url = `/users?page=${page}&pageSize=${pageSize}`;
      if (username) url += `&username=${encodeURIComponent(username)}`;
      if (realName) url += `&realName=${encodeURIComponent(realName)}`;
      if (roleIdFilter) url += `&roleId=${roleIdFilter}`;
      if (statusFilter !== null) url += `&status=${statusFilter}`;
      const res = await api.get<{ list: UserRow[]; total: number }>(url);
      if (res.code === 0 && res.data) {
        setList(res.data.list);
        setTotal(res.data.total);
      }
    } finally {
      setLoading(false);
    }
  };

  const loadRoles = async () => {
    const res = await api.get<RoleOption[]>('/users/roles');
    if (res.code === 0 && res.data) setRoles(res.data);
  };

  useEffect(() => {
    loadRoles();
  }, []);
  useEffect(() => {
    load();
  }, [page, pageSize]);

  const onSearch = () => {
    setPage(1);
    load();
  };

  const onAdd = async (v: Record<string, unknown>) => {
    const res = await api.post<{ id: string }>('/users', {
      username: v.username,
      realName: v.realName,
      password: v.password,
      roleId: v.roleId || undefined
    });
    if (res.code === 0) {
      message.success('新增成功');
      setAddModalOpen(false);
      form.resetFields();
      load();
    } else {
      message.error(res.message || '新增失败');
    }
  };

  const openEdit = (record: UserRow) => {
    setEditingId(record.id);
    editForm.setFieldsValue({
      realName: record.realName,
      roleId: record.roleId || undefined,
      status: record.status
    });
    setEditModalOpen(true);
  };

  const onEdit = async (v: Record<string, unknown>) => {
    if (!editingId) return;
    const body: Record<string, unknown> = {
      realName: v.realName,
      roleId: v.roleId || undefined,
      status: v.status
    };
    if (v.newPassword) body.newPassword = v.newPassword;
    const res = await api.put<unknown>(`/users/${editingId}`, body);
    if (res.code === 0) {
      message.success('修改成功');
      setEditModalOpen(false);
      setEditingId(null);
      editForm.resetFields();
      load();
    } else {
      message.error(res.message || '修改失败');
    }
  };

  const onDelete = async (id: string) => {
    const res = await api.delete<unknown>(`/users/${id}`);
    if (res.code === 0) {
      message.success('已禁用该用户');
      load();
    } else {
      message.error(res.message || '操作失败');
    }
  };

  return (
    <div>
      <h2 style={{ marginBottom: 24 }}>用户管理</h2>
      <Space style={{ marginBottom: 16 }} wrap>
        <Input
          placeholder="用户名"
          value={username}
          onChange={e => setUsername(e.target.value)}
          onPressEnter={onSearch}
          style={{ width: 140 }}
        />
        <Input
          placeholder="姓名"
          value={realName}
          onChange={e => setRealName(e.target.value)}
          onPressEnter={onSearch}
          style={{ width: 140 }}
        />
        <Select
          placeholder="角色"
          allowClear
          style={{ width: 140 }}
          value={roleIdFilter}
          onChange={v => setRoleIdFilter(v)}
          options={roles.map(r => ({ value: r.id, label: r.name }))}
        />
        <Select
          placeholder="状态"
          allowClear
          style={{ width: 100 }}
          value={statusFilter}
          onChange={v => setStatusFilter(v)}
          options={STATUS_OPTIONS}
        />
        <Button type="primary" onClick={onSearch}>查询</Button>
        <Button icon={<PlusOutlined />} onClick={() => setAddModalOpen(true)}>新增用户</Button>
      </Space>
      <Table
        rowKey="id"
        loading={loading}
        dataSource={list}
        columns={[
          { title: '用户名', dataIndex: 'username', key: 'username' },
          { title: '姓名', dataIndex: 'realName', key: 'realName' },
          {
            title: '角色',
            dataIndex: 'roleName',
            key: 'roleName',
            render: (t: string) => t ? <Tag color="blue">{t}</Tag> : '-'
          },
          {
            title: '状态',
            dataIndex: 'status',
            key: 'status',
            render: (s: number) =>
              s === 1 ? <Tag color="green">正常</Tag> : <Tag color="red">禁用</Tag>
          },
          {
            title: '创建时间',
            dataIndex: 'createdAt',
            key: 'createdAt',
            render: (t: string) => new Date(t).toLocaleString()
          },
          {
            title: '操作',
            key: 'action',
            width: 160,
            render: (_: unknown, record: UserRow) => (
              <Space>
                <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(record)}>
                  编辑
                </Button>
                <Popconfirm
                  title="确定要禁用该用户吗？禁用后无法登录。"
                  onConfirm={() => onDelete(record.id)}
                  okText="确定"
                  cancelText="取消"
                >
                  <Button type="link" size="small" danger icon={<DeleteOutlined />}>
                    禁用
                  </Button>
                </Popconfirm>
              </Space>
            )
          }
        ]}
        pagination={{
          current: page,
          pageSize,
          total,
          showSizeChanger: true,
          showTotal: (t) => `共 ${t} 条`
        }}
        onChange={p => {
          setPage(p.current || 1);
          setPageSize(p.pageSize || 20);
        }}
      />

      {/* 新增用户 */}
      <Modal
        title="新增用户"
        open={addModalOpen}
        onCancel={() => { setAddModalOpen(false); form.resetFields(); }}
        footer={null}
      >
        <Form form={form} onFinish={onAdd} layout="vertical">
          <Form.Item name="username" label="用户名" rules={[{ required: true, message: '请输入用户名' }]}>
            <Input placeholder="登录用，不可重复" />
          </Form.Item>
          <Form.Item name="realName" label="姓名">
            <Input placeholder="显示名称" />
          </Form.Item>
          <Form.Item name="password" label="初始密码" rules={[{ required: true, message: '请输入密码' }, { min: 6, message: '至少 6 位' }]}>
            <Input.Password placeholder="至少 6 位" />
          </Form.Item>
          <Form.Item name="roleId" label="角色（权限设置）">
            <Select
              placeholder="选择角色"
              allowClear
              options={roles.map(r => ({ value: r.id, label: `${r.name} (${r.code})` }))}
            />
          </Form.Item>
          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit">确定</Button>
              <Button onClick={() => { setAddModalOpen(false); form.resetFields(); }}>取消</Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>

      {/* 编辑用户 / 权限设置 */}
      <Modal
        title="编辑用户 / 权限设置"
        open={editModalOpen}
        onCancel={() => { setEditModalOpen(false); setEditingId(null); editForm.resetFields(); }}
        footer={null}
      >
        <Form form={editForm} onFinish={onEdit} layout="vertical">
          <Form.Item name="realName" label="姓名">
            <Input placeholder="显示名称" />
          </Form.Item>
          <Form.Item name="roleId" label="角色（权限设置）">
            <Select
              placeholder="选择角色"
              allowClear
              options={roles.map(r => ({ value: r.id, label: `${r.name} (${r.code})` }))}
            />
          </Form.Item>
          <Form.Item name="status" label="状态" rules={[{ required: true }]}>
            <Select options={STATUS_OPTIONS} />
          </Form.Item>
          <Form.Item name="newPassword" label="新密码（不修改请留空）" rules={[{ min: 6, message: '至少 6 位' }]}>
            <Input.Password placeholder="留空则不修改，填写则至少 6 位" />
          </Form.Item>
          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit">保存</Button>
              <Button onClick={() => { setEditModalOpen(false); setEditingId(null); editForm.resetFields(); }}>取消</Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
