import { useState, useEffect } from 'react';
import { Table, Button, Space, Input, Select, message, Modal, Form, Popconfirm, Collapse } from 'antd';
import { PlusOutlined, EditOutlined } from '@ant-design/icons';
import { api } from '../../services/api';

interface ProductRow {
  id: string;
  merchantShopId: string;
  shopName?: string;
  sku?: string;
  name: string;
  nameEn?: string;
  spec?: string;
  imageUrl?: string;
  platformUrl?: string;
  weightKg?: number;
  weightGrams?: number;
  lengthCm?: number;
  widthCm?: number;
  heightCm?: number;
  link1688?: string;
  customerLink?: string;
  factoryLink?: string;
  material?: string;
  styleName?: string;
  sizeChart?: string;
  packageNote?: string;
  suggestedPurchasePrice?: number;
  suggestedSalePrice?: number;
  remark?: string;
  status: number;
  createdAt: string;
}

interface ShopOption {
  id: string;
  name: string;
  platform?: string;
}

export default function ProductList() {
  const [list, setList] = useState<ProductRow[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [loading, setLoading] = useState(false);
  const [shopId, setShopId] = useState<string | null>(null);
  const [skuFilter, setSkuFilter] = useState('');
  const [nameFilter, setNameFilter] = useState('');
  const [shops, setShops] = useState<ShopOption[]>([]);
  const [modalOpen, setModalOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form] = Form.useForm();

  const loadShops = async () => {
    const res = await api.get<{ list: ShopOption[] }>('/shops?pageSize=500');
    if (res.code === 0 && res.data?.list) setShops(res.data.list);
  };

  const load = async () => {
    setLoading(true);
    try {
      let url = `/products?page=${page}&pageSize=${pageSize}`;
      if (shopId) url += `&shopId=${shopId}`;
      if (skuFilter) url += `&sku=${encodeURIComponent(skuFilter)}`;
      if (nameFilter) url += `&name=${encodeURIComponent(nameFilter)}`;
      const res = await api.get<{ list: ProductRow[]; total: number }>(url);
      if (res.code === 0 && res.data) {
        setList(res.data.list);
        setTotal(res.data.total);
      }
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { loadShops(); }, []);
  useEffect(() => { load(); }, [page, pageSize, shopId]);

  const onSearch = () => { setPage(1); load(); };

  const openAdd = () => {
    setEditingId(null);
    form.setFieldsValue({
      merchantShopId: shopId || undefined,
      sku: '', name: '', nameEn: '', spec: '', imageUrl: '', platformUrl: '',
      weightKg: undefined, weightGrams: undefined, lengthCm: undefined, widthCm: undefined, heightCm: undefined,
      link1688: '', customerLink: '', factoryLink: '', material: '', styleName: '', sizeChart: '', packageNote: '',
      suggestedPurchasePrice: undefined, suggestedSalePrice: undefined, remark: ''
    });
    setModalOpen(true);
  };

  const openEdit = (record: ProductRow) => {
    setEditingId(record.id);
    form.setFieldsValue({
      sku: record.sku, name: record.name, nameEn: record.nameEn, spec: record.spec, imageUrl: record.imageUrl, platformUrl: record.platformUrl,
      weightKg: record.weightKg, weightGrams: record.weightGrams, lengthCm: record.lengthCm, widthCm: record.widthCm, heightCm: record.heightCm,
      link1688: record.link1688, customerLink: record.customerLink, factoryLink: record.factoryLink,
      material: record.material, styleName: record.styleName, sizeChart: record.sizeChart, packageNote: record.packageNote,
      suggestedPurchasePrice: record.suggestedPurchasePrice, suggestedSalePrice: record.suggestedSalePrice, remark: record.remark
    });
    setModalOpen(true);
  };

  const payload = (v: Record<string, unknown>) => ({
    sku: v.sku, name: v.name, nameEn: v.nameEn, spec: v.spec, imageUrl: v.imageUrl, platformUrl: v.platformUrl,
    weightKg: v.weightKg, weightGrams: v.weightGrams, lengthCm: v.lengthCm, widthCm: v.widthCm, heightCm: v.heightCm,
    link1688: v.link1688, customerLink: v.customerLink, factoryLink: v.factoryLink,
    material: v.material, styleName: v.styleName, sizeChart: v.sizeChart, packageNote: v.packageNote,
    suggestedPurchasePrice: v.suggestedPurchasePrice, suggestedSalePrice: v.suggestedSalePrice, remark: v.remark
  });

  const onFinish = async (v: Record<string, unknown>) => {
    if (editingId) {
      const res = await api.put<unknown>(`/products/${editingId}`, payload(v));
      if (res.code === 0) {
        message.success('修改成功');
        setModalOpen(false);
        load();
      } else {
        message.error(res.message || '修改失败');
      }
    } else {
      if (!v.merchantShopId) { message.error('请选择店铺'); return; }
      const res = await api.post<{ id: string }>('/products', { merchantShopId: v.merchantShopId, ...payload(v) });
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
    const res = await api.delete<unknown>(`/products/${id}`);
    if (res.code === 0) {
      message.success('已停用');
      load();
    } else {
      message.error(res.message || '操作失败');
    }
  };

  return (
    <div>
      <h2 style={{ marginBottom: 24 }}>商品管理</h2>
      <Space style={{ marginBottom: 16 }} wrap>
        <Select
          placeholder="店铺"
          allowClear
          style={{ width: 220 }}
          value={shopId}
          onChange={setShopId}
          options={shops.map(s => ({ value: s.id, label: `${s.name}${s.platform ? ` (${s.platform})` : ''}` }))}
        />
        <Input placeholder="SKU" value={skuFilter} onChange={e => setSkuFilter(e.target.value)} onPressEnter={onSearch} style={{ width: 140 }} />
        <Input placeholder="商品名称" value={nameFilter} onChange={e => setNameFilter(e.target.value)} onPressEnter={onSearch} style={{ width: 160 }} />
        <Button type="primary" onClick={onSearch}>查询</Button>
        <Button icon={<PlusOutlined />} onClick={openAdd}>新增商品</Button>
      </Space>
      <Table
        rowKey="id"
        loading={loading}
        dataSource={list}
        scroll={{ x: 1200 }}
        columns={[
          { title: '店铺', dataIndex: 'shopName', key: 'shopName', width: 120 },
          { title: 'SKU', dataIndex: 'sku', key: 'sku', width: 100 },
          { title: '商品名称', dataIndex: 'name', key: 'name', ellipsis: true },
          { title: '规格', dataIndex: 'spec', key: 'spec', width: 80 },
          { title: '英文名', dataIndex: 'nameEn', key: 'nameEn', width: 100, ellipsis: true },
          { title: '重量(g)', dataIndex: 'weightGrams', key: 'weightGrams', width: 85, render: (v: number) => v != null ? v : '-' },
          { title: '尺寸(cm)', key: 'dims', width: 100, render: (_: unknown, r: ProductRow) => [r.lengthCm, r.widthCm, r.heightCm].some(x => x != null) ? `${r.lengthCm ?? '-'}×${r.widthCm ?? '-'}×${r.heightCm ?? '-'}` : '-' },
          { title: '1688链接', dataIndex: 'link1688', key: 'link1688', width: 80, ellipsis: true, render: (t: string) => t ? <a href={t} target="_blank" rel="noreferrer">链接</a> : '-' },
          { title: '建议采购价', dataIndex: 'suggestedPurchasePrice', key: 'suggestedPurchasePrice', width: 100, render: (v: number) => v != null ? v : '-' },
          { title: '建议售价', dataIndex: 'suggestedSalePrice', key: 'suggestedSalePrice', width: 90, render: (v: number) => v != null ? v : '-' },
          { title: '状态', dataIndex: 'status', key: 'status', width: 70, render: (s: number) => s === 1 ? '正常' : '停用' },
          {
            title: '操作',
            key: 'action',
            width: 140,
            fixed: 'right' as const,
            render: (_: unknown, r: ProductRow) => (
              <Space>
                <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(r)}>编辑</Button>
                {r.status === 1 && (
                  <Popconfirm title="确定停用该商品？" onConfirm={() => onDelete(r.id)}>
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
      <Modal title={editingId ? '编辑商品' : '新增商品'} open={modalOpen} onCancel={() => setModalOpen(false)} footer={null} width={640}>
        <Form form={form} onFinish={onFinish} layout="vertical">
          {!editingId && (
            <Form.Item name="merchantShopId" label="所属店铺" rules={[{ required: true, message: '请选择店铺' }]}>
              <Select placeholder="选择店铺" options={shops.map(s => ({ value: s.id, label: `${s.name}${s.platform ? ` (${s.platform})` : ''}` }))} />
            </Form.Item>
          )}
          <Form.Item name="sku" label="SKU">
            <Input placeholder="商品编码" />
          </Form.Item>
          <Form.Item name="name" label="商品名称（中文）" rules={[{ required: true, message: '请输入商品名称' }]}>
            <Input />
          </Form.Item>
          <Form.Item name="nameEn" label="商品名称（英文）">
            <Input placeholder="用于报价表" />
          </Form.Item>
          <Form.Item name="spec" label="规格">
            <Input placeholder="如 颜色/尺码" />
          </Form.Item>
          <Form.Item name="imageUrl" label="主图链接">
            <Input placeholder="图片URL" />
          </Form.Item>
          <Form.Item name="platformUrl" label="平台商品链接">
            <Input placeholder="店铺商品页" />
          </Form.Item>
          <Collapse size="small" items={[
            {
              key: 'weight',
              label: '重量与尺寸（用于运费计算）',
              children: (
                <>
                  <Space style={{ width: '100%' }} wrap>
                    <Form.Item name="weightKg" label="重量(kg)">
                      <Input type="number" step={0.01} placeholder="kg" style={{ width: 100 }} />
                    </Form.Item>
                    <Form.Item name="weightGrams" label="重量(g)">
                      <Input type="number" step={1} placeholder="克" style={{ width: 100 }} />
                    </Form.Item>
                  </Space>
                  <Space style={{ width: '100%' }} wrap>
                    <Form.Item name="lengthCm" label="长(cm)">
                      <Input type="number" step={0.1} style={{ width: 90 }} />
                    </Form.Item>
                    <Form.Item name="widthCm" label="宽(cm)">
                      <Input type="number" step={0.1} style={{ width: 90 }} />
                    </Form.Item>
                    <Form.Item name="heightCm" label="高(cm)">
                      <Input type="number" step={0.1} style={{ width: 90 }} />
                    </Form.Item>
                  </Space>
                  <Form.Item name="packageNote" label="包装说明">
                    <Input placeholder="如 包装尺寸32*20.8*2.5" />
                  </Form.Item>
                </>
              )
            },
            {
              key: 'links',
              label: '找货/报价链接',
              children: (
                <>
                  <Form.Item name="link1688" label="1688采购链接">
                    <Input placeholder="1688商品链接" />
                  </Form.Item>
                  <Form.Item name="customerLink" label="客户链接">
                    <Input placeholder="客户商品页" />
                  </Form.Item>
                  <Form.Item name="factoryLink" label="厂家链接">
                    <Input placeholder="厂家链接" />
                  </Form.Item>
                  <Form.Item name="material" label="材质">
                    <Input />
                  </Form.Item>
                  <Form.Item name="styleName" label="款式">
                    <Input />
                  </Form.Item>
                  <Form.Item name="sizeChart" label="尺码表">
                    <Input placeholder="尺码表说明或链接" />
                  </Form.Item>
                </>
              )
            }
          ]} />
          <Space style={{ width: '100%' }} align="baseline">
            <Form.Item name="suggestedPurchasePrice" label="建议采购价">
              <Input type="number" step={0.01} placeholder="元" style={{ width: 120 }} />
            </Form.Item>
            <Form.Item name="suggestedSalePrice" label="建议售价">
              <Input type="number" step={0.01} placeholder="元" style={{ width: 120 }} />
            </Form.Item>
          </Space>
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
