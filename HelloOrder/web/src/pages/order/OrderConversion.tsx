import { useState, useEffect } from 'react';
import {
  Card,
  Button,
  Table,
  Space,
  DatePicker,
  Input,
  message,
  Modal,
  Form,
  Upload,
  Drawer,
  Tag,
  Popconfirm,
  Alert,
  Select
} from 'antd';
import { PlusOutlined, UploadOutlined, FileExcelOutlined, DownloadOutlined, EditOutlined, DeleteOutlined, EyeOutlined } from '@ant-design/icons';
import { api } from '../../services/api';
import { getStoredToken } from '../../store/AuthContext';

const API_BASE = '/api';

interface MerchantOption { id: string; name: string; }
interface ShopOption { id: string; name: string; merchantId: string; }

interface JobRow {
  id: string;
  title: string;
  quotationFileName?: string;
  trackingFileName?: string;
  extractedDate?: string;
  merchantFolder?: string;
  merchantId?: string;
  merchantShopId?: string;
  merchantName?: string;
  merchantShopName?: string;
  createdAt: string;
  remark?: string;
  quotationSheetCount: number;
  trackingSheetCount: number;
}

interface JobDetail extends JobRow {
  quotationSheets: { id: string; sheetName: string; sheetType: number }[];
  trackingSheets: { id: string; sheetName: string }[];
}

export default function OrderConversion() {
  const [list, setList] = useState<JobRow[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [loading, setLoading] = useState(false);
  const [fromDate, setFromDate] = useState<string | null>(null);
  const [toDate, setToDate] = useState<string | null>(null);
  const [titleFilter, setTitleFilter] = useState('');
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [detailDrawerOpen, setDetailDrawerOpen] = useState(false);
  const [detailJob, setDetailJob] = useState<JobDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [editingJob, setEditingJob] = useState<JobDetail | null>(null);
  const [convertLoading, setConvertLoading] = useState<string | null>(null);
  const [form] = Form.useForm();
  const [editForm] = Form.useForm();
  const [merchants, setMerchants] = useState<MerchantOption[]>([]);
  const [shops, setShops] = useState<ShopOption[]>([]);

  const loadList = async () => {
    setLoading(true);
    try {
      let url = `/order-conversion/jobs?page=${page}&pageSize=${pageSize}`;
      if (fromDate) url += `&from=${encodeURIComponent(fromDate)}`;
      if (toDate) url += `&to=${encodeURIComponent(toDate)}`;
      if (titleFilter) url += `&title=${encodeURIComponent(titleFilter)}`;
      const res = await api.get<{ list: JobRow[]; total: number }>(url);
      if (res.code === 0 && res.data) {
        setList(res.data.list);
        setTotal(res.data.total);
      }
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { loadList(); }, [page, pageSize]);

  useEffect(() => {
    (async () => {
      const res = await api.get<{ list: MerchantOption[] }>('/merchants?pageSize=500');
      if (res.code === 0 && res.data?.list) setMerchants(res.data.list);
    })();
  }, []);

  const loadShops = async (merchantId: string | undefined) => {
    if (!merchantId) { setShops([]); return; }
    const res = await api.get<{ list: ShopOption[] }>(`/shops?merchantId=${merchantId}&pageSize=500`);
    if (res.code === 0 && res.data?.list) setShops(res.data.list);
    else setShops([]);
  };

  const onSearch = () => { setPage(1); loadList(); };

  const loadDetail = async (id: string) => {
    setDetailLoading(true);
    try {
      const res = await api.get<JobDetail>(`/order-conversion/jobs/${id}`);
      if (res.code === 0 && res.data) setDetailJob(res.data);
    } finally {
      setDetailLoading(false);
    }
  };

  const openDetail = (id: string) => {
    setDetailDrawerOpen(true);
    setDetailJob(null);
    loadDetail(id);
  };

  const handleCreate = async (v: { title?: string; merchantFolder?: string; merchantId?: string; merchantShopId?: string; remark?: string }) => {
    if (!v.merchantId) { message.error('请选择商家'); return; }
    const res = await api.post<{ id: string }>('/order-conversion/jobs', {
      title: v.title || '未命名',
      merchantFolder: v.merchantFolder,
      merchantId: v.merchantId,
      merchantShopId: v.merchantShopId || undefined,
      remark: v.remark
    });
    const id = (res.data as { id?: string })?.id ?? res.data;
    if (res.code === 0 && id) {
      message.success('已创建');
      setCreateModalOpen(false);
      form.resetFields();
      loadList();
      openDetail(typeof id === 'string' ? id : String(id));
    } else {
      message.error(res.message || '创建失败');
    }
  };

  const handleEdit = (job: JobDetail) => {
    setEditingJob(job);
    editForm.setFieldsValue({
      title: job.title,
      merchantFolder: job.merchantFolder,
      merchantId: job.merchantId || undefined,
      merchantShopId: job.merchantShopId || undefined,
      remark: job.remark
    });
    setEditModalOpen(true);
    if (job.merchantId) loadShops(job.merchantId);
    else setShops([]);
  };

  const saveEdit = async (v: { title?: string; merchantFolder?: string; merchantId?: string; merchantShopId?: string; remark?: string }) => {
    if (!editingJob) return;
    const res = await api.put<unknown>(`/order-conversion/jobs/${editingJob.id}`, v);
    if (res.code === 0) {
      message.success('已保存');
      setEditModalOpen(false);
      setEditingJob(null);
      loadList();
      if (detailJob?.id === editingJob.id) loadDetail(editingJob.id);
    } else {
      message.error(res.message || '保存失败');
    }
  };

  const handleDelete = async (id: string) => {
    const res = await api.delete<unknown>(`/order-conversion/jobs/${id}`);
    if (res.code === 0) {
      message.success('已删除');
      setDetailDrawerOpen(false);
      setDetailJob(null);
      loadList();
    } else {
      message.error(res.message || '删除失败');
    }
  };

  const uploadQuotation = async (jobId: string, file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    const token = getStoredToken();
    const res = await fetch(`${API_BASE}/order-conversion/jobs/${jobId}/upload-quotation`, {
      method: 'POST',
      headers: token ? { Authorization: `Bearer ${token}` } : {},
      body: formData
    });
    const json = await res.json();
    if (json.code === 0) {
      message.success('报价新文件上传并解析成功');
      loadDetail(jobId);
      loadList();
    } else {
      message.error(json.message || '上传失败');
    }
  };

  const uploadTracking = async (jobId: string, file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    const token = getStoredToken();
    const res = await fetch(`${API_BASE}/order-conversion/jobs/${jobId}/upload-tracking`, {
      method: 'POST',
      headers: token ? { Authorization: `Bearer ${token}` } : {},
      body: formData
    });
    const json = await res.json();
    if (json.code === 0) {
      message.success('Tracking&Cost 文件上传并解析成功');
      loadDetail(jobId);
      loadList();
    } else {
      message.error(json.message || '上传失败');
    }
  };

  const runConvert = async (jobId: string) => {
    setConvertLoading(jobId);
    try {
      const res = await api.post<{
        success: boolean;
        message?: string;
        ordersSynced?: number;
        orderItemsSynced?: number;
        syncWarning?: string;
      }>(`/order-conversion/jobs/${jobId}/convert`, {});
      if (res.code === 0 && res.data?.success) {
        const msg = res.data.message || '转换成功';
        if (res.data.ordersSynced != null && res.data.ordersSynced > 0) {
          message.success(`${msg} 已同步 ${res.data.ordersSynced} 笔订单到订单管理。`);
        } else if (res.data.syncWarning) {
          message.warning(`${msg} ${res.data.syncWarning}`);
        } else {
          message.success(msg);
        }
        if (detailJob?.id === jobId) loadDetail(jobId);
      } else {
        message.warning(res.data?.message || res.message || '转换未完成');
      }
    } finally {
      setConvertLoading(null);
    }
  };

  const downloadExport = async (jobId: string, type: 'only-ship' | 'total' | 'purchase') => {
    const token = getStoredToken();
    const res = await fetch(`${API_BASE}/order-conversion/jobs/${jobId}/export/${type}`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {}
    });
    if (!res.ok) {
      message.error('导出失败或文件不存在');
      return;
    }
    const blob = await res.blob();
    const name = res.headers.get('Content-Disposition')?.match(/filename="?([^";]+)"?/)?.[1] || `export-${type}.xlsx`;
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = name;
    a.click();
    URL.revokeObjectURL(a.href);
  };

  return (
    <div>
      <h2 style={{ marginBottom: 24 }}>订单文件转换</h2>
      <Card>
        <Alert
          type="info"
          showIcon
          message="创建转换任务时请选择商家（必填），上传「报价新」与「Tracking&Cost」Excel 后执行转换。转换成功后会自动将订单同步到「订单管理」页，可在该页查看与管理。并可导出 onlyShip / Total / 采购 三份 Excel。"
          style={{ marginBottom: 16 }}
        />
        <Space style={{ marginBottom: 16 }} wrap>
          <DatePicker.RangePicker
            allowClear
            onChange={(_, s) => { setFromDate(s?.[0] ?? null); setToDate(s?.[1] ?? null); }}
          />
          <Input placeholder="标题筛选" value={titleFilter} onChange={e => setTitleFilter(e.target.value)} onPressEnter={onSearch} style={{ width: 160 }} />
          <Button type="primary" onClick={onSearch}>查询</Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => { setCreateModalOpen(true); form.resetFields(); }}>新建任务</Button>
        </Space>
        <Table
          rowKey="id"
          loading={loading}
          dataSource={list}
          scroll={{ x: 900 }}
          columns={[
            { title: '标题', dataIndex: 'title', key: 'title', ellipsis: true, width: 140 },
            { title: '商家', dataIndex: 'merchantName', key: 'merchantName', ellipsis: true, width: 100, render: (t: string) => t || '-' },
            { title: '报价新文件', dataIndex: 'quotationFileName', key: 'quotationFileName', ellipsis: true, width: 140, render: (t: string) => t || '-' },
            { title: 'Tracking文件', dataIndex: 'trackingFileName', key: 'trackingFileName', ellipsis: true, width: 140, render: (t: string) => t || '-' },
            { title: '报价Sheet数', dataIndex: 'quotationSheetCount', key: 'q', width: 100, render: (n: number) => n || 0 },
            { title: 'Tracking Sheet数', dataIndex: 'trackingSheetCount', key: 't', width: 120, render: (n: number) => n || 0 },
            { title: '创建时间', dataIndex: 'createdAt', key: 'createdAt', width: 180, render: (t: string) => t ? new Date(t).toLocaleString() : '-' },
            {
              title: '操作',
              key: 'action',
              width: 200,
              fixed: 'right',
              render: (_: unknown, r: JobRow) => (
                <Space size="small" wrap>
                  <Button type="link" size="small" icon={<EyeOutlined />} onClick={() => openDetail(r.id)}>查看</Button>
                  <Button type="link" size="small" icon={<EditOutlined />} onClick={() => handleEdit(r as JobDetail)}>编辑</Button>
                  <Popconfirm title="确定删除该任务及所有数据？" onConfirm={() => handleDelete(r.id)}>
                    <Button type="link" size="small" danger icon={<DeleteOutlined />}>删除</Button>
                  </Popconfirm>
                </Space>
              )
            }
          ]}
          pagination={{ current: page, pageSize, total, showSizeChanger: true, showTotal: t => `共 ${t} 条` }}
          onChange={p => { setPage(p.current || 1); setPageSize(p.pageSize || 20); }}
        />
      </Card>

      <Modal title="新建转换任务" open={createModalOpen} onCancel={() => setCreateModalOpen(false)} footer={null}>
        <Form form={form} onFinish={handleCreate} layout="vertical">
          <Form.Item name="title" label="标题" rules={[{ required: true, message: '请输入标题' }]}>
            <Input placeholder="如 LG-Le 2026.1.22" />
          </Form.Item>
          <Form.Item name="merchantId" label="商家" rules={[{ required: true, message: '请选择商家（同步到订单管理必填）' }]}>
            <Select
              placeholder="选择商家"
              allowClear
              showSearch
              optionFilterProp="label"
              options={merchants.map(m => ({ value: m.id, label: m.name }))}
              onChange={() => { form.setFieldValue('merchantShopId', undefined); loadShops(form.getFieldValue('merchantId')); }}
            />
          </Form.Item>
          <Form.Item name="merchantShopId" label="店铺（可选）">
            <Select
              placeholder="选择店铺"
              allowClear
              showSearch
              optionFilterProp="label"
              options={shops.map(s => ({ value: s.id, label: s.name }))}
              disabled={!form.getFieldValue('merchantId')}
            />
          </Form.Item>
          <Form.Item name="merchantFolder" label="商户目录">
            <Input placeholder="如 LG-Le" />
          </Form.Item>
          <Form.Item name="remark" label="备注">
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit">创建</Button>
              <Button onClick={() => setCreateModalOpen(false)}>取消</Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>

      <Modal title="编辑任务" open={editModalOpen} onCancel={() => { setEditModalOpen(false); setEditingJob(null); }} footer={null}>
        <Form form={editForm} onFinish={saveEdit} layout="vertical">
          <Form.Item name="title" label="标题" rules={[{ required: true, message: '请输入标题' }]}>
            <Input />
          </Form.Item>
          <Form.Item name="merchantId" label="商家" rules={[{ required: true, message: '请选择商家（同步到订单管理必填）' }]}>
            <Select
              placeholder="选择商家"
              allowClear
              showSearch
              optionFilterProp="label"
              options={merchants.map(m => ({ value: m.id, label: m.name }))}
              onChange={(v) => { editForm.setFieldValue('merchantShopId', undefined); loadShops(v ?? undefined); }}
            />
          </Form.Item>
          <Form.Item name="merchantShopId" label="店铺（可选）">
            <Select
              placeholder="选择店铺"
              allowClear
              showSearch
              optionFilterProp="label"
              options={shops.map(s => ({ value: s.id, label: s.name }))}
              disabled={!editForm.getFieldValue('merchantId')}
            />
          </Form.Item>
          <Form.Item name="merchantFolder" label="商户目录">
            <Input />
          </Form.Item>
          <Form.Item name="remark" label="备注">
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit">保存</Button>
              <Button onClick={() => { setEditModalOpen(false); setEditingJob(null); }}>取消</Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>

      <Drawer
        title="转换任务详情"
        width={560}
        open={detailDrawerOpen}
        onClose={() => { setDetailDrawerOpen(false); setDetailJob(null); }}
        extra={detailJob && (
          <Popconfirm title="确定删除？" onConfirm={() => handleDelete(detailJob.id)}>
            <Button danger icon={<DeleteOutlined />}>删除</Button>
          </Popconfirm>
        )}
      >
        {detailLoading && <p>加载中...</p>}
        {!detailLoading && detailJob && (
          <>
            <p><strong>标题：</strong>{detailJob.title}</p>
            <p><strong>报价新文件：</strong>{detailJob.quotationFileName || '-'}</p>
            <p><strong>Tracking 文件：</strong>{detailJob.trackingFileName || '-'}</p>
            <p><strong>创建时间：</strong>{new Date(detailJob.createdAt).toLocaleString()}</p>
            {detailJob.remark && <p><strong>备注：</strong>{detailJob.remark}</p>}
            <div style={{ marginTop: 16 }}>
              <div style={{ marginBottom: 8 }}><strong>上传报价新 Excel</strong></div>
              <Upload accept=".xlsx,.xls" maxCount={1} beforeUpload={(file) => { uploadQuotation(detailJob.id, file); return false; }}>
                <Button icon={<UploadOutlined />}>选择报价新 xlsx</Button>
              </Upload>
              <div style={{ marginTop: 12, marginBottom: 8 }}><strong>上传 Tracking&Cost Excel</strong></div>
              <Upload accept=".xlsx,.xls" maxCount={1} beforeUpload={(file) => { uploadTracking(detailJob.id, file); return false; }}>
                <Button icon={<UploadOutlined />}>选择 Tracking xlsx</Button>
              </Upload>
            </div>
            <div style={{ marginTop: 16 }}>
              <strong>报价 Sheet：</strong>
              {detailJob.quotationSheets.length === 0 ? ' 暂无' : detailJob.quotationSheets.map(s => (
                <Tag key={s.id}>{s.sheetName} ({s.sheetType === 0 ? '找货' : s.sheetType === 2 ? 'upsell' : '主报价'})</Tag>
              ))}
            </div>
            <div style={{ marginTop: 8 }}>
              <strong>Tracking Sheet：</strong>
              {detailJob.trackingSheets.length === 0 ? ' 暂无' : detailJob.trackingSheets.map(s => <Tag key={s.id}>{s.sheetName}</Tag>)}
            </div>
            <div style={{ marginTop: 24 }}>
              <Button type="primary" icon={<FileExcelOutlined />} loading={convertLoading === detailJob.id} onClick={() => runConvert(detailJob.id)} style={{ marginRight: 8 }}>
                执行转换
              </Button>
              <Button icon={<EditOutlined />} onClick={() => handleEdit(detailJob)}>编辑</Button>
            </div>
            <div style={{ marginTop: 16 }}>
              <strong>导出：</strong>
              <Space style={{ marginTop: 8 }}>
                <Button icon={<DownloadOutlined />} onClick={() => downloadExport(detailJob.id, 'only-ship')}>onlyShip</Button>
                <Button icon={<DownloadOutlined />} onClick={() => downloadExport(detailJob.id, 'total')}>Total</Button>
                <Button icon={<DownloadOutlined />} onClick={() => downloadExport(detailJob.id, 'purchase')}>采购表</Button>
              </Space>
            </div>
          </>
        )}
      </Drawer>
    </div>
  );
}
