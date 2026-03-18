import { useState, useEffect } from 'react';
import { Table, Button, Space, Input, Select, message, Modal, Form, Popconfirm } from 'antd';
import { PlusOutlined, EditOutlined } from '@ant-design/icons';
import { api } from '../../services/api';

interface ShopRow {
  id: string;
  merchantId: string;
  merchantName?: string;
  name: string;
  platform?: string;
  shopUrl?: string;
  remark?: string;
  status: number;
  createdAt: string;
}

interface MerchantOption {
  id: string;
  name: string;
}

export default function ShopList() {
  const [list, setList] = useState<ShopRow[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [loading, setLoading] = useState(false);
  const [merchantId, setMerchantId] = useState<string | null>(null);
  const [nameFilter, setNameFilter] = useState('');
  const [merchants, setMerchants] = useState<MerchantOption[]>([]);
  const [modalOpen, setModalOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form] = Form.useForm();

  const loadMerchants = async () => {
    const res = await api.get<{ list: MerchantOption[] }>('/merchants?pageSize=500');
    if (res.code === 0 && res.data?.list) setMerchants(res.data.list);
  };

  const load = async () => {
    setLoading(true);
    try {
      let url = `/shops?page=${page}&pageSize=${pageSize}`;
      if (merchantId) url += `&merchantId=${merchantId}`;
      if (nameFilter) url += `&name=${encodeURIComponent(nameFilter)}`;
      const res = await api.get<{ list: ShopRow[]; total: number }>(url);
      if (res.code === 0 && res.data) {
        setList(res.data.list);
        setTotal(res.data.total);
      }
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { loadMerchants(); }, []);
  useEffect(() => { load(); }, [page, pageSize, merchantId]);

  const onSearch = () => { setPage(1); load(); };

  const openAdd = () => {
    setEditingId(null);
    form.setFieldsValue({ merchantId: merchantId || undefined, name: '', platform: '', shopUrl: '', remark: '' });
    setModalOpen(true);
  };

  const openEdit = (record: ShopRow) => {
    setEditingId(record.id);
    form.setFieldsValue({ name: record.name, platform: record.platform, shopUrl: record.shopUrl, remark: record.remark });
    setModalOpen(true);
  };

  const onFinish = async (v: Record<string, unknown>) => {
    if (editingId) {
      const res = await api.put<unknown>(`/shops/${editingId}`, { name: v.name, platform: v.platform, shopUrl: v.shopUrl, remark: v.remark });
      if (res.code === 0) {
        message.success('修改成功');
        setModalOpen(false);
        load();
      } else {
        message.error(res.message || '修改失败');
      }
    } else {
      if (!v.merchantId) { message.error('请选择商家'); return; }
      const res = await api.post<{ id: string }>('/shops', { merchantId: v.merchantId, name: v.name, platform: v.platform, shopUrl: v.shopUrl, remark: v.remark });
      if (res.code === 0) {
        message.success('新增成功');
        setModalOpen(false);
        form.resetFields();
        load();
      } else {
        message.error(res.message || '新增失败');
      }
    }
  };

  const onDelete = async (id: string) => {
    const res = await api.delete<unknown>(`/shops/${id}`);
    if (res.code === 0) {
      message.success('已停用');
      load();
    } else {
      message.error(res.message || '操作失败');
    }
  };

  return (
    <div>
      <h2 style={{ marginBottom: 24 }}>商家店铺</h2>
      <Space style={{ marginBottom: 16 }} wrap>
        <Select
          placeholder="商家"
          allowClear
          style={{ width: 200 }}
          value={merchantId}
          onChange={setMerchantId}
          options={merchants.map(m => ({ value: m.id, label: m.name }))}
        />
        <Input placeholder="店铺名称" value={nameFilter} onChange={e => setNameFilter(e.target.value)} onPressEnter={onSearch} style={{ width: 160 }} />
        <Button type="primary" onClick={onSearch}>查询</Button>
        <Button icon={<PlusOutlined />} onClick={openAdd}>新增店铺</Button>
      </Space>
      <Table
        rowKey="id"
        loading={loading}
        dataSource={list}
        columns={[
          { title: '商家', dataIndex: 'merchantName', key: 'merchantName' },
          { title: '店铺名称', dataIndex: 'name', key: 'name' },
          { title: '平台', dataIndex: 'platform', key: 'platform' },
          { title: '店铺链接', dataIndex: 'shopUrl', key: 'shopUrl', ellipsis: true },
          { title: '备注', dataIndex: 'remark', key: 'remark', ellipsis: true },
          { title: '状态', dataIndex: 'status', key: 'status', render: (s: number) => s === 1 ? '正常' : '停用' },
          { title: '创建时间', dataIndex: 'createdAt', key: 'createdAt', render: (t: string) => new Date(t).toLocaleString() },
          {
            title: '操作',
            key: 'action',
            width: 140,
            render: (_: unknown, r: ShopRow) => (
              <Space>
                <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(r)}>编辑</Button>
                {r.status === 1 && (
                  <Popconfirm title="确定停用该店铺？" onConfirm={() => onDelete(r.id)}>
                    <Button type="link" size="small" danger>停用</Button>
                  </Popconfirm>
                )}
              </Space>
            )
          }
        ]}
        pagination={{ current: page, pageSize, total, showSizeChanger: true, showTotal: (t) => `共 ${t} 条` }}
        onChange={p => { setPage(p.current || 1); setPageSize(p.pageSize || 20); }}
      />
      <Modal title={editingId ? '编辑店铺' : '新增店铺'} open={modalOpen} onCancel={() => setModalOpen(false)} footer={null}>
        <Form form={form} onFinish={onFinish} layout="vertical">
          {!editingId && (
            <Form.Item name="merchantId" label="所属商家" rules={[{ required: true, message: '请选择商家' }]}>
              <Select placeholder="选择商家" options={merchants.map(m => ({ value: m.id, label: m.name }))} />
            </Form.Item>
          )}
          <Form.Item name="name" label="店铺名称" rules={[{ required: true, message: '请输入店铺名称' }]}>
            <Input />
          </Form.Item>
          <Form.Item name="platform" label="平台">
            <Input placeholder="如 Shopee、Lazada" />
          </Form.Item>
          <Form.Item name="shopUrl" label="店铺链接">
            <Input placeholder="店铺地址" />
          </Form.Item>
          <Form.Item name="remark" label="备注">
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit">保存</Button>
              <Button onClick={() => setModalOpen(false)}>取消</Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
