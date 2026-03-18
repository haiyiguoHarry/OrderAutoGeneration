import { useState, useEffect } from 'react';
import { Table, Button, Space, Input, Select, message, Modal, Form, Tabs, Popconfirm } from 'antd';
import { PlusOutlined, EditOutlined } from '@ant-design/icons';
import { api } from '../../services/api';

interface ExpressCompanyRow {
  id: string;
  code?: string;
  name: string;
  contact?: string;
  remark?: string;
  status: number;
  createdAt: string;
  updatedAt: string;
}

interface ExpressRateRow {
  id: string;
  expressCompanyId: string;
  expressCompanyName?: string;
  countryCode: string;
  countryName?: string;
  unitPrice?: number;
  leadDaysMin?: number;
  leadDaysMax?: number;
  chargeRule?: string;
  remark?: string;
}

const COUNTRY_OPTIONS = [
  { value: 'FR', label: '法国 FR' },
  { value: 'BE', label: '比利时 BE' },
  { value: 'CH', label: '瑞士 CH' },
  { value: 'LU', label: '卢森堡 LU' },
  { value: 'IT', label: '意大利 IT' },
  { value: 'DE', label: '德国 DE' },
  { value: 'ES', label: '西班牙 ES' },
];

export default function ExpressList() {
  const [companies, setCompanies] = useState<ExpressCompanyRow[]>([]);
  const [companiesTotal, setCompaniesTotal] = useState(0);
  const [companyPage, setCompanyPage] = useState(1);
  const [companyOptions, setCompanyOptions] = useState<ExpressCompanyRow[]>([]);
  const [rates, setRates] = useState<ExpressRateRow[]>([]);
  const [ratesTotal, setRatesTotal] = useState(0);
  const [ratePage, setRatePage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [companyModalOpen, setCompanyModalOpen] = useState(false);
  const [rateModalOpen, setRateModalOpen] = useState(false);
  const [editingCompanyId, setEditingCompanyId] = useState<string | null>(null);
  const [editingRateId, setEditingRateId] = useState<string | null>(null);
  const [rateCompanyFilter, setRateCompanyFilter] = useState<string | null>(null);
  const [companyForm] = Form.useForm();
  const [rateForm] = Form.useForm();

  const loadCompanies = async () => {
    setLoading(true);
    try {
      const res = await api.get<{ list: ExpressCompanyRow[]; total: number }>(`/express-companies?page=${companyPage}&pageSize=20&status=1`);
      if (res.code === 0 && res.data) {
        setCompanies(res.data.list);
        setCompaniesTotal(res.data.total);
      }
    } finally {
      setLoading(false);
    }
  };

  const loadCompanyOptions = async () => {
    const res = await api.get<{ list: ExpressCompanyRow[] }>('/express-companies?page=1&pageSize=500&status=1');
    if (res.code === 0 && res.data?.list) setCompanyOptions(res.data.list);
  };

  const loadRates = async () => {
    setLoading(true);
    try {
      let url = `/express-country-rates?page=${ratePage}&pageSize=20`;
      if (rateCompanyFilter) url += `&companyId=${rateCompanyFilter}`;
      const res = await api.get<{ list: ExpressRateRow[]; total: number }>(url);
      if (res.code === 0 && res.data) {
        setRates(res.data.list);
        setRatesTotal(res.data.total);
      }
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { loadCompanyOptions(); }, []);
  useEffect(() => { loadCompanies(); }, [companyPage]);
  useEffect(() => { loadRates(); }, [ratePage, rateCompanyFilter]);

  const openAddCompany = () => {
    setEditingCompanyId(null);
    companyForm.setFieldsValue({ code: '', name: '', contact: '', remark: '' });
    setCompanyModalOpen(true);
  };

  const openEditCompany = (r: ExpressCompanyRow) => {
    setEditingCompanyId(r.id);
    companyForm.setFieldsValue({ code: r.code, name: r.name, contact: r.contact, remark: r.remark });
    setCompanyModalOpen(true);
  };

  const onCompanyFinish = async (v: Record<string, unknown>) => {
    if (editingCompanyId) {
      const res = await api.put<unknown>(`/express-companies/${editingCompanyId}`, v);
      if (res.code === 0) {
        message.success('修改成功');
        setCompanyModalOpen(false);
        loadCompanies();
      } else {
        message.error(res.message || '修改失败');
      }
    } else {
      const res = await api.post<{ id: string }>('/express-companies', v);
      if (res.code === 0) {
        message.success('新增成功');
        setCompanyModalOpen(false);
        loadCompanies();
      } else {
        message.error(res.message || '新增失败');
      }
    }
  };

  const openAddRate = () => {
    setEditingRateId(null);
    rateForm.setFieldsValue({
      expressCompanyId: rateCompanyFilter || (companyOptions[0]?.id),
      countryCode: undefined,
      countryName: '',
      unitPrice: undefined,
      leadDaysMin: undefined,
      leadDaysMax: undefined,
      chargeRule: '',
      remark: ''
    });
    setRateModalOpen(true);
  };

  const openEditRate = (r: ExpressRateRow) => {
    setEditingRateId(r.id);
    rateForm.setFieldsValue({
      countryCode: r.countryCode,
      countryName: r.countryName,
      unitPrice: r.unitPrice,
      leadDaysMin: r.leadDaysMin,
      leadDaysMax: r.leadDaysMax,
      chargeRule: r.chargeRule,
      remark: r.remark
    });
    setRateModalOpen(true);
  };

  const onRateFinish = async (v: Record<string, unknown>) => {
    if (editingRateId) {
      const res = await api.put<unknown>(`/express-country-rates/${editingRateId}`, {
        countryCode: v.countryCode,
        countryName: v.countryName,
        unitPrice: v.unitPrice,
        leadDaysMin: v.leadDaysMin,
        leadDaysMax: v.leadDaysMax,
        chargeRule: v.chargeRule,
        remark: v.remark
      });
      if (res.code === 0) {
        message.success('修改成功');
        setRateModalOpen(false);
        loadRates();
      } else {
        message.error(res.message || '修改失败');
      }
    } else {
      if (!v.expressCompanyId) { message.error('请选择快递公司'); return; }
      const res = await api.post<{ id: string }>('/express-country-rates', {
        expressCompanyId: v.expressCompanyId,
        countryCode: v.countryCode,
        countryName: v.countryName,
        unitPrice: v.unitPrice,
        leadDaysMin: v.leadDaysMin,
        leadDaysMax: v.leadDaysMax,
        chargeRule: v.chargeRule,
        remark: v.remark
      });
      if (res.code === 0) {
        message.success('新增成功');
        setRateModalOpen(false);
        loadRates();
      } else {
        message.error(res.message || '新增失败');
      }
    }
  };

  const onDeleteCompany = async (id: string) => {
    const res = await api.delete<unknown>(`/express-companies/${id}`);
    if (res.code === 0) {
      message.success('已停用');
      loadCompanies();
    } else {
      message.error(res.message || '操作失败');
    }
  };

  const onDeleteRate = async (id: string) => {
    const res = await api.delete<unknown>(`/express-country-rates/${id}`);
    if (res.code === 0) {
      message.success('已删除');
      loadRates();
    } else {
      message.error(res.message || '操作失败');
    }
  };

  return (
    <div>
      <h2 style={{ marginBottom: 24 }}>快递与物流</h2>
      <Tabs
        items={[
          {
            key: 'companies',
            label: '快递公司',
            children: (
              <>
                <Space style={{ marginBottom: 16 }}>
                  <Button type="primary" icon={<PlusOutlined />} onClick={openAddCompany}>新增快递公司</Button>
                </Space>
                <Table
                  rowKey="id"
                  loading={loading}
                  dataSource={companies}
                  columns={[
                    { title: '编码', dataIndex: 'code', key: 'code', width: 100 },
                    { title: '名称', dataIndex: 'name', key: 'name' },
                    { title: '联系方式', dataIndex: 'contact', key: 'contact', width: 160 },
                    { title: '备注', dataIndex: 'remark', key: 'remark', ellipsis: true },
                    {
                      title: '操作',
                      key: 'action',
                      width: 160,
                      render: (_: unknown, r: ExpressCompanyRow) => (
                        <Space>
                          <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEditCompany(r)}>编辑</Button>
                          {r.status === 1 && (
                            <Popconfirm title="确定停用？" onConfirm={() => onDeleteCompany(r.id)}>
                              <Button type="link" size="small" danger>停用</Button>
                            </Popconfirm>
                          )}
                        </Space>
                      )
                    }
                  ]}
                  pagination={{ current: companyPage, pageSize: 20, total: companiesTotal, showTotal: (t) => `共 ${t} 条`, onChange: (p) => setCompanyPage(p || 1) }}
                />
              </>
            )
          },
          {
            key: 'rates',
            label: '国家运费',
            children: (
              <>
                <Space style={{ marginBottom: 16 }} wrap>
                  <Select
                    placeholder="按快递公司筛选"
                    allowClear
                    style={{ width: 200 }}
                    value={rateCompanyFilter}
                    onChange={setRateCompanyFilter}
                    options={companyOptions.map(c => ({ value: c.id, label: c.name }))}
                  />
                  <Button type="primary" icon={<PlusOutlined />} onClick={openAddRate}>新增国家运费</Button>
                </Space>
                <Table
                  rowKey="id"
                  loading={loading}
                  dataSource={rates}
                  columns={[
                    { title: '快递公司', dataIndex: 'expressCompanyName', key: 'expressCompanyName', width: 120 },
                    { title: '国家码', dataIndex: 'countryCode', key: 'countryCode', width: 80 },
                    { title: '国家名', dataIndex: 'countryName', key: 'countryName', width: 100 },
                    { title: '单价', dataIndex: 'unitPrice', key: 'unitPrice', width: 90, render: (v: number) => v != null ? v : '-' },
                    { title: '时效(天)', key: 'lead', width: 100, render: (_: unknown, r: ExpressRateRow) => [r.leadDaysMin, r.leadDaysMax].some(x => x != null) ? `${r.leadDaysMin ?? '-'}-${r.leadDaysMax ?? '-'}` : '-' },
                    { title: '计费规则', dataIndex: 'chargeRule', key: 'chargeRule', width: 90 },
                    {
                      title: '操作',
                      key: 'action',
                      width: 140,
                      render: (_: unknown, r: ExpressRateRow) => (
                        <Space>
                          <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEditRate(r)}>编辑</Button>
                          <Popconfirm title="确定删除？" onConfirm={() => onDeleteRate(r.id)}>
                            <Button type="link" size="small" danger>删除</Button>
                          </Popconfirm>
                        </Space>
                      )
                    }
                  ]}
                  pagination={{ current: ratePage, pageSize: 20, total: ratesTotal, showTotal: (t) => `共 ${t} 条`, onChange: (p) => setRatePage(p || 1) }}
                />
              </>
            )
          }
        ]}
      />
      <Modal title={editingCompanyId ? '编辑快递公司' : '新增快递公司'} open={companyModalOpen} onCancel={() => setCompanyModalOpen(false)} footer={null}>
        <Form form={companyForm} onFinish={onCompanyFinish} layout="vertical">
          <Form.Item name="code" label="编码">
            <Input placeholder="可选" />
          </Form.Item>
          <Form.Item name="name" label="名称" rules={[{ required: true, message: '请输入名称' }]}>
            <Input />
          </Form.Item>
          <Form.Item name="contact" label="联系方式">
            <Input />
          </Form.Item>
          <Form.Item name="remark" label="备注">
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit">保存</Button>
              <Button onClick={() => setCompanyModalOpen(false)}>取消</Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>
      <Modal title={editingRateId ? '编辑国家运费' : '新增国家运费'} open={rateModalOpen} onCancel={() => setRateModalOpen(false)} footer={null}>
        <Form form={rateForm} onFinish={onRateFinish} layout="vertical">
          {!editingRateId && (
            <Form.Item name="expressCompanyId" label="快递公司" rules={[{ required: true, message: '请选择快递公司' }]}>
              <Select placeholder="选择快递公司" options={companies.map(c => ({ value: c.id, label: c.name }))} />
            </Form.Item>
          )}
          <Form.Item name="countryCode" label="国家码" rules={[{ required: true, message: '请选择或输入国家码' }]}>
            <Select placeholder="选择或输入" options={COUNTRY_OPTIONS} showSearch optionFilterProp="label" allowClear />
          </Form.Item>
          <Form.Item name="countryName" label="国家名">
            <Input placeholder="如 法国" />
          </Form.Item>
          <Form.Item name="unitPrice" label="单价">
            <Input type="number" step={0.01} placeholder="运费单价" />
          </Form.Item>
          <Space style={{ width: '100%' }} wrap>
            <Form.Item name="leadDaysMin" label="时效最少(天)">
              <Input type="number" placeholder="如 6" style={{ width: 100 }} />
            </Form.Item>
            <Form.Item name="leadDaysMax" label="时效最多(天)">
              <Input type="number" placeholder="如 11" style={{ width: 100 }} />
            </Form.Item>
          </Space>
          <Form.Item name="chargeRule" label="计费规则">
            <Input placeholder="如 按重量/体积" />
          </Form.Item>
          <Form.Item name="remark" label="备注">
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit">保存</Button>
              <Button onClick={() => setRateModalOpen(false)}>取消</Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
