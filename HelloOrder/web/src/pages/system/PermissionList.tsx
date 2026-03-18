import { useState, useEffect } from 'react';
import { Table, Button, Space, Input, Select, message, Modal, Form, Popconfirm } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { api } from '../../services/api';

interface PermissionRow {
  id: string;
  code: string;
  name: string;
  type: number;
  parentId?: string;
  path?: string;
  sort: number;
}

const TYPE_OPTIONS = [
  { value: 0, label: '菜单' },
  { value: 1, label: '按钮' },
  { value: 2, label: '接口' }
];

export default function PermissionList() {
  const [list, setList] = useState<PermissionRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [typeFilter, setTypeFilter] = useState<number | null>(null);
  const [nameFilter, setNameFilter] = useState('');
  const [codeFilter, setCodeFilter] = useState('');

  const [addModalOpen, setAddModalOpen] = useState(false);
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form] = Form.useForm();
  const [editForm] = Form.useForm();

  const load = async () => {
    setLoading(true);
    try {
      let url = '/permissions?';
      if (typeFilter !== null) url += `type=${typeFilter}&`;
      if (nameFilter) url += `name=${encodeURIComponent(nameFilter)}&`;
      if (codeFilter) url += `code=${encodeURIComponent(codeFilter)}&`;
      const res = await api.get<PermissionRow[]>(url);
      if (res.code === 0 && res.data) setList(res.data);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const onSearch = () => load();

  const onAdd = async (v: Record<string, unknown>) => {
    const res = await api.post<{ id: string }>('/permissions', {
      code: v.code,
      name: v.name,
      type: v.type ?? 0,
      path: v.path || undefined,
      sort: Number(v.sort) || 0
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

  const openEdit = (record: PermissionRow) => {
    setEditingId(record.id);
    editForm.setFieldsValue({
      name: record.name,
      type: record.type,
      path: record.path,
      sort: record.sort
    });
    setEditModalOpen(true);
  };

  const onEdit = async (v: Record<string, unknown>) => {
    if (!editingId) return;
    const res = await api.put<unknown>(`/permissions/${editingId}`, {
      name: v.name,
      type: v.type,
      path: v.path ?? '',
      sort: Number(v.sort) || 0
    });
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
    const res = await api.delete<unknown>(`/permissions/${id}`);
    if (res.code === 0) {
      message.success('删除成功');
      load();
    } else {
      message.error(res.message || '删除失败');
    }
  };

  return (
    <div>
      <h2 style={{ marginBottom: 24 }}>页面权限管理</h2>
      <Space style={{ marginBottom: 16 }} wrap>
        <Input placeholder="权限编码" value={codeFilter} onChange={e => setCodeFilter(e.target.value)} onPressEnter={onSearch} style={{ width: 140 }} />
        <Input placeholder="权限名称" value={nameFilter} onChange={e => setNameFilter(e.target.value)} onPressEnter={onSearch} style={{ width: 140 }} />
        <Select placeholder="类型" allowClear style={{ width: 100 }} value={typeFilter} onChange={setTypeFilter} options={TYPE_OPTIONS} />
        <Button type="primary" onClick={onSearch}>查询</Button>
        <Button icon={<PlusOutlined />} onClick={() => setAddModalOpen(true)}>新增权限</Button>
      </Space>
      <Table
        rowKey="id"
        loading={loading}
        dataSource={list}
        columns={[
          { title: '权限编码', dataIndex: 'code', key: 'code' },
          { title: '权限名称', dataIndex: 'name', key: 'name' },
          { title: '类型', dataIndex: 'type', key: 'type', render: (t: number) => TYPE_OPTIONS.find(o => o.value === t)?.label ?? t },
          { title: '路径', dataIndex: 'path', key: 'path', ellipsis: true },
          { title: '排序', dataIndex: 'sort', key: 'sort', width: 80 },
          {
            title: '操作',
            key: 'action',
            width: 160,
            render: (_: unknown, record: PermissionRow) => (
              <Space>
                <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(record)}>编辑</Button>
                <Popconfirm title="确定删除该权限吗？若已被角色引用需先在角色权限中取消。" onConfirm={() => onDelete(record.id)} okText="确定" cancelText="取消">
                  <Button type="link" size="small" danger icon={<DeleteOutlined />}>删除</Button>
                </Popconfirm>
              </Space>
            )
          }
        ]}
      />

      <Modal title="新增权限" open={addModalOpen} onCancel={() => { setAddModalOpen(false); form.resetFields(); }} footer={null}>
        <Form form={form} onFinish={onAdd} layout="vertical">
          <Form.Item name="code" label="权限编码" rules={[{ required: true, message: '请输入权限编码' }]}>
            <Input placeholder="如 menu:xxx，不可重复" />
          </Form.Item>
          <Form.Item name="name" label="权限名称">
            <Input placeholder="显示名称" />
          </Form.Item>
          <Form.Item name="type" label="类型" initialValue={0}>
            <Select options={TYPE_OPTIONS} />
          </Form.Item>
          <Form.Item name="path" label="路径">
            <Input placeholder="如 /dashboard" />
          </Form.Item>
          <Form.Item name="sort" label="排序" initialValue={0}>
            <Input type="number" />
          </Form.Item>
          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit">确定</Button>
              <Button onClick={() => { setAddModalOpen(false); form.resetFields(); }}>取消</Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>

      <Modal title="编辑权限" open={editModalOpen} onCancel={() => { setEditModalOpen(false); setEditingId(null); editForm.resetFields(); }} footer={null}>
        <Form form={editForm} onFinish={onEdit} layout="vertical">
          <Form.Item name="name" label="权限名称">
            <Input />
          </Form.Item>
          <Form.Item name="type" label="类型">
            <Select options={TYPE_OPTIONS} />
          </Form.Item>
          <Form.Item name="path" label="路径">
            <Input placeholder="如 /dashboard" />
          </Form.Item>
          <Form.Item name="sort" label="排序">
            <Input type="number" />
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
