import { useState, useEffect } from 'react';
import { Table, Button, Space } from 'antd';
import { api } from '../../services/api';

const statusMap: Record<number, string> = {
  0: '待报价',
  1: '已报价',
  2: '已确认待付款',
  3: '已付款',
  4: '已生成采购',
  5: '采购中',
  6: '到仓待发',
  7: '已发货',
  8: '已完成',
  9: '售后'
};

interface OrderRow {
  id: string;
  merchantId: string;
  merchantName?: string;
  merchantShopId?: string;
  merchantShopName?: string;
  orderNo: string;
  status: number;
  totalAmount: number;
  currency: string;
  orderTime?: string;
  createdAt: string;
}

export default function OrderList() {
  const [list, setList] = useState<OrderRow[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [loading, setLoading] = useState(false);

  const load = async () => {
    setLoading(true);
    try {
      const res = await api.get<{ list: OrderRow[]; total: number }>(`/orders?page=${page}&pageSize=${pageSize}`);
      if (res.code === 0 && res.data) {
        setList(res.data.list);
        setTotal(res.data.total);
      }
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, [page, pageSize]);

  return (
    <div>
      <h2 style={{ marginBottom: 24 }}>订单管理</h2>
      <Space style={{ marginBottom: 16 }}>
        <Button type="primary" onClick={load}>刷新</Button>
      </Space>
      <Table
        rowKey="id"
        loading={loading}
        dataSource={list}
        columns={[
          { title: '订单号', dataIndex: 'orderNo', key: 'orderNo' },
          { title: '商家', dataIndex: 'merchantName', key: 'merchantName' },
          { title: '店铺', dataIndex: 'merchantShopName', key: 'merchantShopName', render: (t: string) => t || '-' },
          { title: '状态', dataIndex: 'status', key: 'status', render: (s: number) => statusMap[s] ?? s },
          { title: '金额', dataIndex: 'totalAmount', key: 'totalAmount', render: (v: number, r: OrderRow) => `${r.currency} ${v}` },
          { title: '订单时间', dataIndex: 'orderTime', key: 'orderTime', render: (t: string) => t ? new Date(t).toLocaleString() : '-' },
          { title: '创建时间', dataIndex: 'createdAt', key: 'createdAt', render: (t: string) => new Date(t).toLocaleString() }
        ]}
        pagination={{ current: page, pageSize, total, showSizeChanger: true, showTotal: (t) => `共 ${t} 条` }}
        onChange={p => { setPage(p.current || 1); setPageSize(p.pageSize || 20); }}
      />
    </div>
  );
}
