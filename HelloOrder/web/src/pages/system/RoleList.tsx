import { useState, useEffect } from 'react';
import { Table, Button, Space, Input, message, Modal, Form, Select, Popconfirm, Checkbox } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined, SafetyOutlined } from '@ant-design/icons';
import { api } from '../../services/api';

interface RoleRow {
  id: string;
  code: string;
  name: string;
  dataScope: number;
  createdAt: string;
}

interface PermissionItem {
  id: string;
  code: string;
  name: string;
  type: number;
  path?: string;
  sort: number;
}

const DATA_SCOPE_OPTIONS = [
  { value: 0, label: '全部' },
  { value: 1, label: '本部门' },
  { value: 2, label: '本人' }
];

export default function RoleList() {
  const [list, setList] = useState<RoleRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [codeFilter, setCodeFilter] = useState('');
  const [nameFilter, setNameFilter] = useState('');

  const [addModalOpen, setAddModalOpen] = useState(false);
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [permModalOpen, setPermModalOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [permRoleId, setPermRoleId] = useState<string | null>(null);
  const [permRoleName, setPermRoleName] = useState('');

  const [allPermissions, setAllPermissions] = useState<PermissionItem[]>([]);
  const [rolePermissionIds, setRolePermissionIds] = useState<string[]>([]);
  const [permLoading, setPermLoading] = useState(false);

  const [form] = Form.useForm();
  const [editForm] = Form.useForm();

  const load = async () => {
    setLoading(true);
    try {
      let url = '/roles?';
      if (codeFilter) url += `code=${encodeURIComponent(codeFilter)}&`;
      if (nameFilter) url += `name=${encodeURIComponent(nameFilter)}&`;
      const res = await api.get<RoleRow[]>(url);
      if (res.code === 0 && res.data) setList(res.data);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const onSearch = () => load();

  const onAdd = async (v: Record<string, unknown>) => {
    const res = await api.post<{ id: string }>('/roles', {
      code: v.code,
      name: v.name,
      dataScope: v.dataScope ?? 0
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

  const openEdit = (record: RoleRow) => {
    setEditingId(record.id);
    editForm.setFieldsValue({ name: record.name, dataScope: record.dataScope });
    setEditModalOpen(true);
  };

  const onEdit = async (v: Record<string, unknown>) => {
    if (!editingId) return;
    const res = await api.put<unknown>(`/roles/${editingId}`, { name: v.name, dataScope: v.dataScope });
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
    const res = await api.delete<unknown>(`/roles/${id}`);
    if (res.code === 0) {
      message.success('删除成功');
      load();
    } else {
      message.error(res.message || '删除失败');
    }
  };

  const openPermModal = async (record: RoleRow) => {
    setPermRoleId(record.id);
    setPermRoleName(record.name);
    setPermModalOpen(true);
    setPermLoading(true);
    try {
      const [permRes, rolePermRes] = await Promise.all([
        api.get<PermissionItem[]>('/permissions?type=0'),
        api.get<string[]>('/roles/' + record.id + '/permissions')
      ]);
      if (permRes.code === 0 && permRes.data) setAllPermissions(permRes.data);
      if (rolePermRes.code === 0 && rolePermRes.data) setRolePermissionIds(rolePermRes.data);
    } finally {
      setPermLoading(false);
    }
  };

  const onPermChange = (id: string, checked: boolean) => {
    if (checked) setRolePermissionIds(prev => [...prev, id]);
    else setRolePermissionIds(prev => prev.filter(x => x !== id));
  };

  const onSavePerm = async () => {
    if (!permRoleId) return;
    const res = await api.put<unknown>(`/roles/${permRoleId}/permissions`, {
      permissionIds: rolePermissionIds
    });
    if (res.code === 0) {
      message.success('页面权限已保存');
      setPermModalOpen(false);
      setPermRoleId(null);
      setPermRoleName('');
    } else {
      message.error(res.message || '保存失败');
    }
  };

  return (
    <div>
      <h2 style={{ marginBottom: 24 }}>角色管理</h2>
      <Space style={{ marginBottom: 16 }} wrap>
        <Input placeholder="角色编码" value={codeFilter} onChange={e => setCodeFilter(e.target.value)} onPressEnter={onSearch} style={{ width: 140 }} />
        <Input placeholder="角色名称" value={nameFilter} onChange={e => setNameFilter(e.target.value)} onPressEnter={onSearch} style={{ width: 140 }} />
        <Button type="primary" onClick={onSearch}>查询</Button>
        <Button icon={<PlusOutlined />} onClick={() => setAddModalOpen(true)}>新增角色</Button>
      </Space>
      <Table
        rowKey="id"
        loading={loading}
        dataSource={list}
        columns={[
          { title: '角色编码', dataIndex: 'code', key: 'code' },
          { title: '角色名称', dataIndex: 'name', key: 'name' },
          {
            title: '数据范围',
            dataIndex: 'dataScope',
            key: 'dataScope',
            render: (v: number) => DATA_SCOPE_OPTIONS.find(o => o.value === v)?.label ?? v
          },
          { title: '创建时间', dataIndex: 'createdAt', key: 'createdAt', render: (t: string) => new Date(t).toLocaleString() },
          {
            title: '操作',
            key: 'action',
            width: 220,
            render: (_: unknown, record: RoleRow) => (
              <Space>
                <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(record)}>编辑</Button>
                <Button type="link" size="small" icon={<SafetyOutlined />} onClick={() => openPermModal(record)}>页面权限</Button>
                <Popconfirm title="确定删除该角色吗？若有用户绑定需先解除。" onConfirm={() => onDelete(record.id)} okText="确定" cancelText="取消">
                  <Button type="link" size="small" danger icon={<DeleteOutlined />}>删除</Button>
                </Popconfirm>
              </Space>
            )
          }
        ]}
      />

      <Modal title="新增角色" open={addModalOpen} onCancel={() => { setAddModalOpen(false); form.resetFields(); }} footer={null}>
        <Form form={form} onFinish={onAdd} layout="vertical">
          <Form.Item name="code" label="角色编码" rules={[{ required: true, message: '请输入角色编码' }]}>
            <Input placeholder="如 CustomRole，不可重复" />
          </Form.Item>
          <Form.Item name="name" label="角色名称">
            <Input placeholder="显示名称" />
          </Form.Item>
          <Form.Item name="dataScope" label="数据范围" initialValue={0}>
            <Select options={DATA_SCOPE_OPTIONS} />
          </Form.Item>
          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit">确定</Button>
              <Button onClick={() => { setAddModalOpen(false); form.resetFields(); }}>取消</Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>

      <Modal title="编辑角色" open={editModalOpen} onCancel={() => { setEditModalOpen(false); setEditingId(null); editForm.resetFields(); }} footer={null}>
        <Form form={editForm} onFinish={onEdit} layout="vertical">
          <Form.Item name="name" label="角色名称">
            <Input />
          </Form.Item>
          <Form.Item name="dataScope" label="数据范围">
            <Select options={DATA_SCOPE_OPTIONS} />
          </Form.Item>
          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit">保存</Button>
              <Button onClick={() => { setEditModalOpen(false); setEditingId(null); editForm.resetFields(); }}>取消</Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={`页面权限设置 - ${permRoleName}`}
        open={permModalOpen}
        onCancel={() => { setPermModalOpen(false); setPermRoleId(null); setPermRoleName(''); }}
        onOk={onSavePerm}
        okText="保存"
        width={480}
      >
        {permLoading ? (
          <div style={{ padding: 24, textAlign: 'center' }}>加载中...</div>
        ) : (
          <div style={{ maxHeight: 400, overflow: 'auto' }}>
            {allPermissions.map(p => (
              <div key={p.id} style={{ marginBottom: 8 }}>
                <Checkbox
                  checked={rolePermissionIds.includes(p.id)}
                  onChange={e => onPermChange(p.id, e.target.checked)}
                >
                  {p.name} {p.path && <span style={{ color: '#999' }}>({p.path})</span>}
                </Checkbox>
              </div>
            ))}
            {allPermissions.length === 0 && !permLoading && <div style={{ color: '#999' }}>暂无菜单权限数据</div>}
          </div>
        )}
      </Modal>
    </div>
  );
}
