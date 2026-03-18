import { useState, useEffect } from 'react';
import { Table, Button, Space, Input, message, Modal, Form } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { api } from '../../services/api';

interface Merchant {
  id: string;
  name: string;
  platform?: string;
  shopName?: string;
  contact?: string;
  settlementType?: string;
  status: number;
  createdAt: string;
}

export default function MerchantList() {
  const [list, setList] = useState<Merchant[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [loading, setLoading] = useState(false);
  const [name, setName] = useState('');
  const [modalOpen, setModalOpen] = useState(false);
  const [form] = Form.useForm();

  const load = async () => {
    setLoading(true);
    try {
      const res = await api.get<{ list: Merchant[]; total: number; page: number; pageSize: number }>(
        `/merchants?page=${page}&pageSize=${pageSize}&name=${encodeURIComponent(name)}`
      );
      if (res.code === 0 && res.data) {
        setList(res.data.list);
        setTotal(res.data.total);
      }
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, [page, pageSize]);
  const onSearch = () => { setPage(1); load(); };

  const onAdd = async (v: Record<string, unknown>) => {
    const res = await api.post<unknown>('/merchants', { name: v.name, platform: v.platform, shopName: v.shopName, contact: v.contact, settlementType: v.settlementType });
    if (res.code === 0) {
      message.success('添加成功');
      setModalOpen(false);
      form.resetFields();
      load();
    } else {
      message.error(res.message || '添加失败');
    }
  };

  return (
    <div>
      <h2 style={{ marginBottom: 24 }}>商家管理</h2>
      <Space style={{ marginBottom: 16 }}>
        <Input placeholder="商家名称" value={name} onChange={e => setName(e.target.value)} onPressEnter={onSearch} style={{ width: 200 }} />
        <Button type="primary" onClick={onSearch}>查询</Button>
        <Button icon={<PlusOutlined />} onClick={() => setModalOpen(true)}>新增商家</Button>
      </Space>
      <Table
        rowKey="id"
        loading={loading}
        dataSource={list}
        columns={[
          { title: '商家名称', dataIndex: 'name', key: 'name' },
          { title: '平台', dataIndex: 'platform', key: 'platform' },
          { title: '店铺名', dataIndex: 'shopName', key: 'shopName' },
          { title: '联系人', dataIndex: 'contact', key: 'contact' },
          { title: '结算方式', dataIndex: 'settlementType', key: 'settlementType' },
          { title: '创建时间', dataIndex: 'createdAt', key: 'createdAt', render: (t: string) => t ? new Date(t).toLocaleString() : '-' }
        ]}
        pagination={{ current: page, pageSize, total, showSizeChanger: true, showTotal: (t) => `共 ${t} 条` }}
        onChange={p => { setPage(p.current || 1); setPageSize(p.pageSize || 20); }}
      />
      <Modal title="新增商家" open={modalOpen} onCancel={() => setModalOpen(false)} footer={null}>
        <Form form={form} onFinish={onAdd} layout="vertical">
          <Form.Item name="name" label="商家名称" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="platform" label="平台">
            <Input placeholder="如 Shopee" />
          </Form.Item>
          <Form.Item name="shopName" label="店铺名">
            <Input />
          </Form.Item>
          <Form.Item name="contact" label="联系人">
            <Input />
          </Form.Item>
          <Form.Item name="settlementType" label="结算方式">
            <Input />
          </Form.Item>
          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit">确定</Button>
              <Button onClick={() => setModalOpen(false)}>取消</Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
