--
-- PostgreSQL database cluster dump
--

\restrict BZfTc1FQHSA9dffrpfIUUeldA5rvLr9PuCkIedyaILYrIjdBHhykYbXegmOQAMq

SET default_transaction_read_only = off;

SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;

--
-- Roles
--

CREATE ROLE postgres;
ALTER ROLE postgres WITH SUPERUSER INHERIT CREATEROLE CREATEDB LOGIN REPLICATION BYPASSRLS PASSWORD 'SCRAM-SHA-256$4096:iqnGhh7UEmDsYMiGqgbpPw==$o+uKIgYjjj+yeNxTlTYwmEBFPyEG20WGwuOZBzTaVMU=:+uZmZjPw70nDnMYsQFDjbw/uBiah4J9qZcRL6Qa9wIw=';

--
-- User Configurations
--








\unrestrict BZfTc1FQHSA9dffrpfIUUeldA5rvLr9PuCkIedyaILYrIjdBHhykYbXegmOQAMq

--
-- Databases
--

--
-- Database "template1" dump
--

\connect template1

--
-- PostgreSQL database dump
--

\restrict 4vHJ7pAylGKIoFldswJYcFltrrdgCtUOLG5pyaqn9dR5LHIeHG471Wy36iCHZLy

-- Dumped from database version 16.14
-- Dumped by pg_dump version 16.14

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- PostgreSQL database dump complete
--

\unrestrict 4vHJ7pAylGKIoFldswJYcFltrrdgCtUOLG5pyaqn9dR5LHIeHG471Wy36iCHZLy

--
-- Database "WebPos" dump
--

--
-- PostgreSQL database dump
--

\restrict RFWcVV4ZzHfoepFc93f0gKDaYsf0Rc8gRdg7x09oFdg3YPreB3cymKfuwdKt13e

-- Dumped from database version 16.14
-- Dumped by pg_dump version 16.14

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: WebPos; Type: DATABASE; Schema: -; Owner: postgres
--

CREATE DATABASE "WebPos" WITH TEMPLATE = template0 ENCODING = 'UTF8' LOCALE_PROVIDER = libc LOCALE = 'en_US.utf8';


ALTER DATABASE "WebPos" OWNER TO postgres;

\unrestrict RFWcVV4ZzHfoepFc93f0gKDaYsf0Rc8gRdg7x09oFdg3YPreB3cymKfuwdKt13e
\connect "WebPos"
\restrict RFWcVV4ZzHfoepFc93f0gKDaYsf0Rc8gRdg7x09oFdg3YPreB3cymKfuwdKt13e

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: __EFMigrationsHistory; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL
);


ALTER TABLE public."__EFMigrationsHistory" OWNER TO postgres;

--
-- Name: categories; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.categories (
    id uuid NOT NULL,
    name character varying(100) NOT NULL,
    parent_category_id uuid,
    target_margin_percentage integer NOT NULL,
    show_on_webshop boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    tenant_id uuid NOT NULL
);


ALTER TABLE public.categories OWNER TO postgres;

--
-- Name: products; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.products (
    id uuid NOT NULL,
    category_id uuid,
    name character varying(150) NOT NULL,
    sku character varying(50) NOT NULL,
    barcode character varying(100) NOT NULL,
    brand character varying(100) NOT NULL,
    base_unit character varying(20) NOT NULL,
    conversion_multiplier integer NOT NULL,
    show_on_webshop boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    tenant_id uuid NOT NULL,
    is_deleted boolean DEFAULT false NOT NULL,
    is_loose boolean DEFAULT false NOT NULL,
    short_code character varying(10) DEFAULT ''::character varying NOT NULL,
    min_stock_qty numeric(18,3) DEFAULT 10.0 NOT NULL
);


ALTER TABLE public.products OWNER TO postgres;

--
-- Name: sales_invoices; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.sales_invoices (
    invoice_no character varying(100) NOT NULL,
    shift_id uuid NOT NULL,
    terminal_id uuid,
    cashier_id uuid NOT NULL,
    customer_id uuid,
    total_amount_paisa bigint NOT NULL,
    tax_amount_paisa bigint NOT NULL,
    discount_amount_paisa bigint NOT NULL,
    discount_reason character varying(255),
    receipt_number character varying(100) NOT NULL,
    payment_method character varying(50) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    tenant_id uuid NOT NULL,
    amount_paid_paisa bigint DEFAULT 0 NOT NULL
);


ALTER TABLE public.sales_invoices OWNER TO postgres;

--
-- Name: sales_items; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.sales_items (
    id uuid NOT NULL,
    invoice_no character varying(100) NOT NULL,
    product_id uuid NOT NULL,
    batch_id uuid NOT NULL,
    quantity numeric(12,3) NOT NULL,
    unit_price_paisa bigint NOT NULL,
    discount_applied_paisa bigint NOT NULL,
    tenant_id uuid NOT NULL,
    unit_cost_paisa bigint DEFAULT 0 NOT NULL
);


ALTER TABLE public.sales_items OWNER TO postgres;

--
-- Name: bi_category_profitability; Type: VIEW; Schema: public; Owner: postgres
--

CREATE VIEW public.bi_category_profitability AS
 SELECT si.tenant_id,
    c.id AS category_id,
    c.name AS category_name,
    (round(sum((si.quantity * (si.unit_price_paisa)::numeric))))::bigint AS revenue_paisa,
    (round(sum((si.quantity * (si.unit_cost_paisa)::numeric))))::bigint AS cost_paisa,
    (round((sum((si.quantity * (si.unit_price_paisa)::numeric)) - sum((si.quantity * (si.unit_cost_paisa)::numeric)))))::bigint AS gross_profit_paisa,
        CASE
            WHEN (sum((si.quantity * (si.unit_price_paisa)::numeric)) = (0)::numeric) THEN (0)::numeric
            ELSE round((((sum((si.quantity * (si.unit_price_paisa)::numeric)) - sum((si.quantity * (si.unit_cost_paisa)::numeric))) * 100.0) / sum((si.quantity * (si.unit_price_paisa)::numeric))), 2)
        END AS margin_percent
   FROM (((public.sales_items si
     JOIN public.products p ON ((p.id = si.product_id)))
     JOIN public.categories c ON ((c.id = p.category_id)))
     JOIN public.sales_invoices inv ON (((inv.invoice_no)::text = (si.invoice_no)::text)))
  WHERE (p.category_id IS NOT NULL)
  GROUP BY si.tenant_id, c.id, c.name;


ALTER VIEW public.bi_category_profitability OWNER TO postgres;

--
-- Name: bi_product_profitability; Type: VIEW; Schema: public; Owner: postgres
--

CREATE VIEW public.bi_product_profitability AS
 SELECT si.tenant_id,
    si.product_id,
    p.name AS product_name,
    (round(sum((si.quantity * (si.unit_price_paisa)::numeric))))::bigint AS revenue_paisa,
    (round(sum((si.quantity * (si.unit_cost_paisa)::numeric))))::bigint AS cost_paisa,
    (round((sum((si.quantity * (si.unit_price_paisa)::numeric)) - sum((si.quantity * (si.unit_cost_paisa)::numeric)))))::bigint AS gross_profit_paisa,
        CASE
            WHEN (sum((si.quantity * (si.unit_price_paisa)::numeric)) = (0)::numeric) THEN (0)::numeric
            ELSE round((((sum((si.quantity * (si.unit_price_paisa)::numeric)) - sum((si.quantity * (si.unit_cost_paisa)::numeric))) * 100.0) / sum((si.quantity * (si.unit_price_paisa)::numeric))), 2)
        END AS margin_percent,
    min(inv.created_at) AS first_sale_at,
    max(inv.created_at) AS last_sale_at
   FROM ((public.sales_items si
     JOIN public.products p ON ((p.id = si.product_id)))
     JOIN public.sales_invoices inv ON (((inv.invoice_no)::text = (si.invoice_no)::text)))
  GROUP BY si.tenant_id, si.product_id, p.name;


ALTER VIEW public.bi_product_profitability OWNER TO postgres;

--
-- Name: cashier_shifts; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.cashier_shifts (
    id uuid NOT NULL,
    terminal_id uuid NOT NULL,
    cashier_id uuid NOT NULL,
    opened_at timestamp with time zone NOT NULL,
    closed_at timestamp with time zone,
    opening_cash_paisa bigint NOT NULL,
    expected_cash_paisa bigint NOT NULL,
    actual_blind_cash_paisa bigint,
    discrepancy_paisa bigint NOT NULL,
    status character varying(20) NOT NULL,
    tenant_id uuid NOT NULL
);


ALTER TABLE public.cashier_shifts OWNER TO postgres;

--
-- Name: daily_milk_collections; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.daily_milk_collections (
    id uuid NOT NULL,
    supplier_id uuid NOT NULL,
    milk_type character varying(20) NOT NULL,
    liters_received numeric(12,3) NOT NULL,
    rate_per_liter_paisa bigint NOT NULL,
    total_credit_paisa bigint NOT NULL,
    collection_time timestamp with time zone NOT NULL,
    tenant_id uuid NOT NULL,
    fat_percent numeric(12,3),
    snf_percent numeric(12,3)
);


ALTER TABLE public.daily_milk_collections OWNER TO postgres;

--
-- Name: damaged_stock_logs; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.damaged_stock_logs (
    id uuid NOT NULL,
    product_id uuid NOT NULL,
    batch_id uuid NOT NULL,
    quantity numeric(12,3) NOT NULL,
    reason_code character varying(50) NOT NULL,
    logged_by uuid NOT NULL,
    write_off_loss_paisa bigint NOT NULL,
    logged_at timestamp with time zone NOT NULL,
    tenant_id uuid NOT NULL
);


ALTER TABLE public.damaged_stock_logs OWNER TO postgres;

--
-- Name: general_ledger_entries; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.general_ledger_entries (
    id uuid NOT NULL,
    transaction_group_id uuid NOT NULL,
    account_code character varying(50) NOT NULL,
    debit_paisa bigint NOT NULL,
    credit_paisa bigint NOT NULL,
    transaction_type character varying(30) NOT NULL,
    reference_no character varying(100) NOT NULL,
    reference_details character varying(255) NOT NULL,
    shift_id uuid,
    party_id uuid,
    created_at timestamp with time zone NOT NULL,
    tenant_id uuid NOT NULL
);


ALTER TABLE public.general_ledger_entries OWNER TO postgres;

--
-- Name: parties; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.parties (
    id uuid NOT NULL,
    party_type character varying(20) NOT NULL,
    name character varying(100) NOT NULL,
    phone_number character varying(20) NOT NULL,
    address text NOT NULL,
    credit_limit_paisa bigint NOT NULL,
    current_balance_paisa bigint NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    tenant_id uuid NOT NULL,
    is_deleted boolean DEFAULT false NOT NULL
);


ALTER TABLE public.parties OWNER TO postgres;

--
-- Name: party_ledgers; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.party_ledgers (
    id uuid NOT NULL,
    party_id uuid NOT NULL,
    invoice_no character varying(100),
    purchase_order_id uuid,
    transaction_type character varying(20) NOT NULL,
    payment_channel character varying(20) NOT NULL,
    old_balance_paisa bigint NOT NULL,
    amount_paisa bigint NOT NULL,
    running_balance_paisa bigint NOT NULL,
    reference_number character varying(100) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    tenant_id uuid NOT NULL,
    credit_paisa bigint DEFAULT 0 NOT NULL,
    debit_paisa bigint DEFAULT 0 NOT NULL
);


ALTER TABLE public.party_ledgers OWNER TO postgres;

--
-- Name: party_payment_allocations; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.party_payment_allocations (
    id uuid NOT NULL,
    party_ledger_id uuid NOT NULL,
    invoice_no character varying(100),
    purchase_order_id uuid,
    amount_paisa bigint NOT NULL,
    created_at timestamp with time zone NOT NULL,
    tenant_id uuid NOT NULL
);


ALTER TABLE public.party_payment_allocations OWNER TO postgres;

--
-- Name: product_batches; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.product_batches (
    id uuid NOT NULL,
    product_id uuid NOT NULL,
    batch_number character varying(100) NOT NULL,
    expiry_date date,
    cost_price_paisa bigint NOT NULL,
    retail_price_paisa bigint NOT NULL,
    initial_qty numeric(12,3) NOT NULL,
    current_qty numeric(12,3) NOT NULL,
    supplier_id uuid NOT NULL,
    rack_location character varying(50),
    purchase_order_id uuid,
    created_at timestamp with time zone NOT NULL,
    tenant_id uuid NOT NULL
);


ALTER TABLE public.product_batches OWNER TO postgres;

--
-- Name: production_consumption_items; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.production_consumption_items (
    id uuid NOT NULL,
    production_log_id uuid NOT NULL,
    product_id uuid NOT NULL,
    quantity_consumed numeric(12,3) NOT NULL,
    tenant_id uuid NOT NULL
);


ALTER TABLE public.production_consumption_items OWNER TO postgres;

--
-- Name: production_logs; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.production_logs (
    id uuid NOT NULL,
    batch_reference character varying(100) NOT NULL,
    operator_id uuid NOT NULL,
    additional_overhead_paisa bigint NOT NULL,
    created_at timestamp with time zone NOT NULL,
    tenant_id uuid NOT NULL
);


ALTER TABLE public.production_logs OWNER TO postgres;

--
-- Name: production_yield_items; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.production_yield_items (
    id uuid NOT NULL,
    production_log_id uuid NOT NULL,
    product_id uuid NOT NULL,
    quantity_produced numeric(12,3) NOT NULL,
    calculated_cost_price_paisa bigint NOT NULL,
    target_batch_id uuid,
    tenant_id uuid NOT NULL
);


ALTER TABLE public.production_yield_items OWNER TO postgres;

--
-- Name: purchase_items; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.purchase_items (
    id uuid NOT NULL,
    purchase_order_id uuid NOT NULL,
    product_id uuid NOT NULL,
    quantity_received numeric(12,3) NOT NULL,
    bonus_quantity numeric(12,3) NOT NULL,
    cost_price_per_unit_paisa bigint NOT NULL,
    retail_price_per_unit_paisa bigint NOT NULL,
    batch_id uuid,
    tenant_id uuid NOT NULL,
    batch_number character varying(100) DEFAULT ''::character varying NOT NULL,
    expiry_date date,
    rack_location character varying(50)
);


ALTER TABLE public.purchase_items OWNER TO postgres;

--
-- Name: purchase_orders; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.purchase_orders (
    id uuid NOT NULL,
    supplier_invoice_no character varying(100) NOT NULL,
    supplier_id uuid NOT NULL,
    receiver_id uuid NOT NULL,
    sub_total_paisa bigint NOT NULL,
    discount_paisa bigint NOT NULL,
    net_payable_paisa bigint NOT NULL,
    payment_status character varying(20) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    tenant_id uuid NOT NULL,
    is_received boolean DEFAULT false NOT NULL,
    amount_paid_paisa bigint DEFAULT 0 NOT NULL
);


ALTER TABLE public.purchase_orders OWNER TO postgres;

--
-- Name: purchase_return_items; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.purchase_return_items (
    id uuid NOT NULL,
    purchase_return_id uuid NOT NULL,
    product_id uuid NOT NULL,
    batch_id uuid NOT NULL,
    quantity numeric(12,3) NOT NULL,
    cost_per_unit_paisa bigint NOT NULL,
    tenant_id uuid NOT NULL
);


ALTER TABLE public.purchase_return_items OWNER TO postgres;

--
-- Name: purchase_returns; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.purchase_returns (
    id uuid NOT NULL,
    original_purchase_order_id uuid NOT NULL,
    manager_id uuid NOT NULL,
    supplier_id uuid NOT NULL,
    total_credit_deduction_paisa bigint NOT NULL,
    created_at timestamp with time zone NOT NULL,
    tenant_id uuid NOT NULL
);


ALTER TABLE public.purchase_returns OWNER TO postgres;

--
-- Name: roles; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.roles (
    id uuid NOT NULL,
    role_name character varying(50) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    tenant_id uuid NOT NULL
);


ALTER TABLE public.roles OWNER TO postgres;

--
-- Name: sales_return_items; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.sales_return_items (
    id uuid NOT NULL,
    sales_return_id uuid NOT NULL,
    product_id uuid NOT NULL,
    batch_id uuid NOT NULL,
    quantity numeric(12,3) NOT NULL,
    refund_unit_price_paisa bigint NOT NULL,
    return_condition character varying(20) NOT NULL,
    tenant_id uuid NOT NULL
);


ALTER TABLE public.sales_return_items OWNER TO postgres;

--
-- Name: sales_returns; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.sales_returns (
    id uuid NOT NULL,
    original_invoice_no character varying(100) NOT NULL,
    cashier_id uuid NOT NULL,
    customer_id uuid,
    total_refund_paisa bigint NOT NULL,
    created_at timestamp with time zone NOT NULL,
    tenant_id uuid NOT NULL
);


ALTER TABLE public.sales_returns OWNER TO postgres;

--
-- Name: shift_expenses; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.shift_expenses (
    id uuid NOT NULL,
    shift_id uuid,
    voucher_no character varying(50) NOT NULL,
    description text NOT NULL,
    amount_paisa bigint NOT NULL,
    expense_category character varying(50) NOT NULL,
    receipt_reference character varying(100) NOT NULL,
    payment_method character varying(20) NOT NULL,
    is_recurring boolean NOT NULL,
    logged_by_user_id uuid NOT NULL,
    logged_at timestamp with time zone NOT NULL,
    tenant_id uuid NOT NULL
);


ALTER TABLE public.shift_expenses OWNER TO postgres;

--
-- Name: tenants; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.tenants (
    id uuid NOT NULL,
    name character varying(100) NOT NULL,
    slug character varying(100) NOT NULL,
    is_active boolean NOT NULL,
    created_at timestamp with time zone NOT NULL
);


ALTER TABLE public.tenants OWNER TO postgres;

--
-- Name: terminals; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.terminals (
    id uuid NOT NULL,
    terminal_name character varying(50) NOT NULL,
    mac_address character varying(100) NOT NULL,
    is_active boolean NOT NULL,
    last_sync_time timestamp with time zone NOT NULL,
    tenant_id uuid NOT NULL
);


ALTER TABLE public.terminals OWNER TO postgres;

--
-- Name: users; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.users (
    id uuid NOT NULL,
    username character varying(50) NOT NULL,
    password_hash character varying(255) NOT NULL,
    role_id uuid NOT NULL,
    is_active boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    pin_hash character varying(64) DEFAULT ''::character varying NOT NULL,
    tenant_id uuid NOT NULL
);


ALTER TABLE public.users OWNER TO postgres;

--
-- Data for Name: __EFMigrationsHistory; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."__EFMigrationsHistory" ("MigrationId", "ProductVersion") FROM stdin;
20260709133919_InitialMigration	10.0.10
20260715135328_AddUserPinHash	10.0.10
20260715143941_EnforceSingleOpenShift	10.0.10
20260716162915_AddMultiTenantFoundation	10.0.10
20260718060713_AddMilkQualityMetrics	10.0.10
20260718153105_TenantScopedPartyUniqueness	10.0.10
20260718155953_PurchaseReceiveAndNullableBatchExpiry	10.0.10
20260719172443_SyncSoftDeleteAndUpdatedAtIndexes	10.0.10
20260722074948_AddProductShortCodeAndIsLoose	10.0.10
20260722144151_AddProductMinStockQty	10.0.10
20260730143910_BiProfitabilityAndPartySettlement	10.0.10
20260730162844_MasterFinanceNullableExpenseShift	10.0.10
\.


--
-- Data for Name: cashier_shifts; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.cashier_shifts (id, terminal_id, cashier_id, opened_at, closed_at, opening_cash_paisa, expected_cash_paisa, actual_blind_cash_paisa, discrepancy_paisa, status, tenant_id) FROM stdin;
397fc0c3-9af8-4e1a-800b-52928df0b0cf	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	2026-07-25 11:13:35.328882+00	2026-07-27 07:59:08.879694+00	0	205400	200000	-5400	CLOSED	00000000-0000-0000-0000-000000000001
e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	2026-07-28 08:44:59.222714+00	2026-08-01 20:31:57.827561+00	500000	744600	800000	55400	CLOSED	00000000-0000-0000-0000-000000000001
0f285329-eb7d-4e22-988e-f66b5b8a304f	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	2026-08-01 20:32:18.816573+00	2026-08-01 20:32:29.359977+00	800000	800000	800000	0	CLOSED	00000000-0000-0000-0000-000000000001
b61e76a2-11dc-46ab-8858-767e63e627e9	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	2026-08-02 06:53:23.961021+00	\N	800000	-200000	\N	0	OPEN	00000000-0000-0000-0000-000000000001
\.


--
-- Data for Name: categories; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.categories (id, name, parent_category_id, target_margin_percentage, show_on_webshop, created_at, tenant_id) FROM stdin;
00000000-0000-0000-0000-000000000401	Dairy	\N	15	f	2026-07-25 11:13:08.767357+00	00000000-0000-0000-0000-000000000001
00000000-0000-0000-0000-000000000402	Grocery	\N	15	f	2026-07-25 11:13:08.767357+00	00000000-0000-0000-0000-000000000001
00000000-0000-0000-0000-000000000403	Bakery	\N	15	f	2026-07-25 11:13:08.767357+00	00000000-0000-0000-0000-000000000001
42128294-2bcc-4bea-91a7-b0786e8c843a	Toys	\N	50	f	2026-07-30 13:56:19.381481+00	00000000-0000-0000-0000-000000000001
0c370f5b-e467-4867-b148-e490eca34bca	Tuck Shop	\N	30	f	2026-08-01 17:00:50.870852+00	00000000-0000-0000-0000-000000000001
f0c93d8f-194d-4546-ba54-f941f5e76d47	Stationary	\N	40	f	2026-08-01 17:01:04.956374+00	00000000-0000-0000-0000-000000000001
8004a68d-195a-4582-a14e-ad8f0a7ae241	Sports	\N	50	f	2026-08-01 17:01:20.988251+00	00000000-0000-0000-0000-000000000001
\.


--
-- Data for Name: daily_milk_collections; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.daily_milk_collections (id, supplier_id, milk_type, liters_received, rate_per_liter_paisa, total_credit_paisa, collection_time, tenant_id, fat_percent, snf_percent) FROM stdin;
\.


--
-- Data for Name: damaged_stock_logs; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.damaged_stock_logs (id, product_id, batch_id, quantity, reason_code, logged_by, write_off_loss_paisa, logged_at, tenant_id) FROM stdin;
\.


--
-- Data for Name: general_ledger_entries; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.general_ledger_entries (id, transaction_group_id, account_code, debit_paisa, credit_paisa, transaction_type, reference_no, reference_details, shift_id, party_id, created_at, tenant_id) FROM stdin;
68407557-e9a9-46ba-a0b4-b97c66e1299b	0ef5427f-2733-4b29-8027-dc6a79e216a7	REVENUE	0	117600	SALE	POS-20260725114229848	Sale invoice POS-20260725114229848	397fc0c3-9af8-4e1a-800b-52928df0b0cf	\N	2026-07-25 11:42:30.021642+00	00000000-0000-0000-0000-000000000001
966a5953-324f-434b-8657-69aae2752012	0ef5427f-2733-4b29-8027-dc6a79e216a7	CASH	117600	0	SALE	POS-20260725114229848	Sale invoice POS-20260725114229848	397fc0c3-9af8-4e1a-800b-52928df0b0cf	\N	2026-07-25 11:42:30.021642+00	00000000-0000-0000-0000-000000000001
429676c6-c4c8-47ee-8cbc-7d89621b2be7	0b3d9e7c-7ad2-40fa-9bdc-65f9f72219aa	ACCOUNTS_PAYABLE	0	3200000	PURCHASE	38368f63-9951-41a4-9f62-1afa8b222360	Purchase receive invoice INV-20260725-1742	\N	00000000-0000-0000-0000-000000000301	2026-07-25 12:43:37.497662+00	00000000-0000-0000-0000-000000000001
e96cec44-d999-44c5-bce0-221a3ad390a6	0b3d9e7c-7ad2-40fa-9bdc-65f9f72219aa	INVENTORY	3200000	0	PURCHASE	38368f63-9951-41a4-9f62-1afa8b222360	Purchase receive invoice INV-20260725-1742	\N	00000000-0000-0000-0000-000000000301	2026-07-25 12:43:37.497662+00	00000000-0000-0000-0000-000000000001
0da5dac5-9596-465e-a41c-3bddd06452fe	571bb4f0-42f5-4969-a387-6dd13b917686	ACCOUNTS_PAYABLE	0	180000	PURCHASE	36295608-81aa-49bd-85fe-d9debebab6c1	Purchase receive invoice INV-20260725-1743	\N	00000000-0000-0000-0000-000000000301	2026-07-25 12:44:42.214242+00	00000000-0000-0000-0000-000000000001
c6fb47a1-0264-482f-bd52-1fae0e2c0442	571bb4f0-42f5-4969-a387-6dd13b917686	INVENTORY	180000	0	PURCHASE	36295608-81aa-49bd-85fe-d9debebab6c1	Purchase receive invoice INV-20260725-1743	\N	00000000-0000-0000-0000-000000000301	2026-07-25 12:44:42.214242+00	00000000-0000-0000-0000-000000000001
0be3e9fa-1ce8-4910-a6cf-d5ca02b0d7e8	027a9580-e54a-4685-84ba-1efc6ee1c82b	CASH	68000	0	SALE	POS-20260726101235813	Sale invoice POS-20260726101235813	397fc0c3-9af8-4e1a-800b-52928df0b0cf	\N	2026-07-26 10:12:35.902522+00	00000000-0000-0000-0000-000000000001
0f6f8521-f105-40ea-ada6-99aafa1d2b86	027a9580-e54a-4685-84ba-1efc6ee1c82b	REVENUE	0	68000	SALE	POS-20260726101235813	Sale invoice POS-20260726101235813	397fc0c3-9af8-4e1a-800b-52928df0b0cf	\N	2026-07-26 10:12:35.902522+00	00000000-0000-0000-0000-000000000001
e4fdac4a-b173-44c2-a413-5044a16cee36	66dd44c5-9829-41a2-841b-d27f6cf47b5e	CASH	19800	0	SALE	POS-20260726101308608	Sale invoice POS-20260726101308608	397fc0c3-9af8-4e1a-800b-52928df0b0cf	00000000-0000-0000-0000-000000000501	2026-07-26 10:13:08.644876+00	00000000-0000-0000-0000-000000000001
f64681e5-347f-419d-b675-a14b616da154	66dd44c5-9829-41a2-841b-d27f6cf47b5e	REVENUE	0	19800	SALE	POS-20260726101308608	Sale invoice POS-20260726101308608	397fc0c3-9af8-4e1a-800b-52928df0b0cf	00000000-0000-0000-0000-000000000501	2026-07-26 10:13:08.644876+00	00000000-0000-0000-0000-000000000001
230e1972-f24c-4407-864c-f47034b8934e	f3f541cf-80d3-4be9-a7bb-0e2addefc8d8	INVENTORY	2360000	0	PURCHASE	aa51afad-b03d-462f-8aae-a5a849fd9429	Purchase receive invoice INV-20260726-1514	\N	00000000-0000-0000-0000-000000000301	2026-07-26 10:15:17.514371+00	00000000-0000-0000-0000-000000000001
b7d4f2ad-4839-4d37-bc1c-640bfc1fd279	f3f541cf-80d3-4be9-a7bb-0e2addefc8d8	ACCOUNTS_PAYABLE	0	2360000	PURCHASE	aa51afad-b03d-462f-8aae-a5a849fd9429	Purchase receive invoice INV-20260726-1514	\N	00000000-0000-0000-0000-000000000301	2026-07-26 10:15:17.514371+00	00000000-0000-0000-0000-000000000001
47f0a178-08ba-4c7b-8cf1-e2c944f269ac	e26e3835-8a5a-4133-81dd-fade10a37472	REVENUE	0	69800	SALE	POS-20260728093244680	Sale invoice POS-20260728093244680	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-28 09:32:45.158297+00	00000000-0000-0000-0000-000000000001
a1db4bd2-70d4-47bb-b399-02231f8c33b6	e26e3835-8a5a-4133-81dd-fade10a37472	CASH	69800	0	SALE	POS-20260728093244680	Sale invoice POS-20260728093244680	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-28 09:32:45.158297+00	00000000-0000-0000-0000-000000000001
151fa76b-c77a-4dc9-93a8-d2e6228df072	442941a7-2afc-44bf-b527-67b26ec5af4e	REVENUE	0	19800	SALE	POS-20260728093434699	Sale invoice POS-20260728093434699	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-28 09:34:34.747193+00	00000000-0000-0000-0000-000000000001
ba0bc3c5-4e0a-4cbf-9299-a2662ce4409f	442941a7-2afc-44bf-b527-67b26ec5af4e	ACCOUNTS_RECEIVABLE	19800	0	SALE	POS-20260728093434699	Sale invoice POS-20260728093434699	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-28 09:34:34.747193+00	00000000-0000-0000-0000-000000000001
7d696945-4d27-4712-a468-8dbe941298ac	e5ce1b57-5d2b-4e9c-80e9-58991e0dec4e	ACCOUNTS_RECEIVABLE	41800	0	SALE	POS-20260728093503740	Sale invoice POS-20260728093503740	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-28 09:35:03.756257+00	00000000-0000-0000-0000-000000000001
7e68ea25-9fe6-4030-ade0-cb1f8e20a0ed	e5ce1b57-5d2b-4e9c-80e9-58991e0dec4e	REVENUE	0	41800	SALE	POS-20260728093503740	Sale invoice POS-20260728093503740	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-28 09:35:03.756257+00	00000000-0000-0000-0000-000000000001
b1dba8be-74e8-4b3d-9786-951937339b68	e5ce1b57-5d2b-4e9c-80e9-58991e0dec4e	REVENUE	41800	0	REVERSAL	POS-20260728093503740	Sales return reversing group e5ce1b57-5d2b-4e9c-80e9-58991e0dec4e for invoice POS-20260728093503740	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-28 09:35:46.882006+00	00000000-0000-0000-0000-000000000001
e343824b-52df-4acc-a0b2-eec28066f2ec	e5ce1b57-5d2b-4e9c-80e9-58991e0dec4e	ACCOUNTS_RECEIVABLE	0	41800	REVERSAL	POS-20260728093503740	Sales return reversing group e5ce1b57-5d2b-4e9c-80e9-58991e0dec4e for invoice POS-20260728093503740	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-28 09:35:46.882006+00	00000000-0000-0000-0000-000000000001
8527515c-f19e-4fab-aba4-655861f62af6	442941a7-2afc-44bf-b527-67b26ec5af4e	ACCOUNTS_RECEIVABLE	0	18450	REVERSAL	POS-20260728093434699	Sales return reversing group 442941a7-2afc-44bf-b527-67b26ec5af4e for invoice POS-20260728093434699	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-28 16:02:02.55786+00	00000000-0000-0000-0000-000000000001
bde9cfef-c1c0-49e7-92de-3239870534df	442941a7-2afc-44bf-b527-67b26ec5af4e	REVENUE	18450	0	REVERSAL	POS-20260728093434699	Sales return reversing group 442941a7-2afc-44bf-b527-67b26ec5af4e for invoice POS-20260728093434699	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-28 16:02:02.55786+00	00000000-0000-0000-0000-000000000001
c7032d80-d1d8-40de-9bab-54c5b3bb1543	0ca86060-f2e3-4735-9ffb-7107fb0ae670	ACCOUNTS_RECEIVABLE	41800	0	SALE	POS-20260728162629268	Sale invoice POS-20260728162629268	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-28 16:26:29.371823+00	00000000-0000-0000-0000-000000000001
e0f6399e-15bf-4e01-b76b-0b191a8194bf	0ca86060-f2e3-4735-9ffb-7107fb0ae670	REVENUE	0	41800	SALE	POS-20260728162629268	Sale invoice POS-20260728162629268	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-28 16:26:29.371823+00	00000000-0000-0000-0000-000000000001
83f6c756-c1e9-437c-b55b-e66fef03f16c	ca331cd5-6044-4d92-9700-cade1ee52202	REVENUE	0	19800	SALE	POS-20260729050714585	Sale invoice POS-20260729050714585	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	\N	2026-07-29 05:07:14.673256+00	00000000-0000-0000-0000-000000000001
fa468050-6a56-46cf-809a-4460d36b57dc	ca331cd5-6044-4d92-9700-cade1ee52202	BANK	19800	0	SALE	POS-20260729050714585	Sale invoice POS-20260729050714585	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	\N	2026-07-29 05:07:14.673256+00	00000000-0000-0000-0000-000000000001
0386dda9-e18b-4ea2-9fdb-57b879b7075e	11e9034f-3a1e-422c-806e-1cfe85a24081	ACCOUNTS_RECEIVABLE	40000	0	SALE	POS-20260729050728574	Sale invoice POS-20260729050728574	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-29 05:07:28.608325+00	00000000-0000-0000-0000-000000000001
cc4f3136-4f18-4f22-b72e-50d0c0591d81	11e9034f-3a1e-422c-806e-1cfe85a24081	REVENUE	0	40000	SALE	POS-20260729050728574	Sale invoice POS-20260729050728574	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-29 05:07:28.608325+00	00000000-0000-0000-0000-000000000001
0ba9376f-58b4-4089-8391-de3566d88d76	eae33901-a345-4e5b-bf37-9485d5986df2	JAZZCASH	40000	0	SALE	POS-20260729050811071	Sale invoice POS-20260729050811071	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-29 05:08:11.088312+00	00000000-0000-0000-0000-000000000001
33f050df-ce1c-4e29-b677-454ee8d5e0f1	eae33901-a345-4e5b-bf37-9485d5986df2	REVENUE	0	40000	SALE	POS-20260729050811071	Sale invoice POS-20260729050811071	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-29 05:08:11.088312+00	00000000-0000-0000-0000-000000000001
7abea074-6a6f-4acc-9a59-e3102b817e23	7a76129e-675b-4c16-8e34-4d7b4cbbe073	REVENUE	0	19800	SALE	POS-20260729050841688	Sale invoice POS-20260729050841688	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	\N	2026-07-29 05:08:41.711072+00	00000000-0000-0000-0000-000000000001
94216e1a-3d0c-4f82-a32a-b10597ba26dc	7a76129e-675b-4c16-8e34-4d7b4cbbe073	CASH	19800	0	SALE	POS-20260729050841688	Sale invoice POS-20260729050841688	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	\N	2026-07-29 05:08:41.711072+00	00000000-0000-0000-0000-000000000001
4fd6487b-3f5e-4236-b266-5e993f811b4c	206ec114-4ffa-48fc-b338-05abff0a721e	ACCOUNTS_RECEIVABLE	1800	0	SALE	POS-20260729052558867	Sale invoice POS-20260729052558867	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-29 05:25:58.917123+00	00000000-0000-0000-0000-000000000001
65332ac3-b9c5-4562-b650-d7843fd18133	206ec114-4ffa-48fc-b338-05abff0a721e	REVENUE	0	1800	SALE	POS-20260729052558867	Sale invoice POS-20260729052558867	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-29 05:25:58.917123+00	00000000-0000-0000-0000-000000000001
5b46e406-aa85-4afb-a4fa-6fb672d56f3b	1fa5d366-8f3f-42c6-9230-45a689bbf195	REVENUE	0	69800	SALE	POS-20260729052638728	Sale invoice POS-20260729052638728	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-29 05:26:38.753603+00	00000000-0000-0000-0000-000000000001
85548572-1a9e-4e6c-b0d0-c1a4f11475b2	1fa5d366-8f3f-42c6-9230-45a689bbf195	ACCOUNTS_RECEIVABLE	69800	0	SALE	POS-20260729052638728	Sale invoice POS-20260729052638728	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-29 05:26:38.753603+00	00000000-0000-0000-0000-000000000001
415bce33-8f88-427a-8c46-dacabf5f4ded	1fa5d366-8f3f-42c6-9230-45a689bbf195	ACCOUNTS_RECEIVABLE	0	69800	REVERSAL	POS-20260729052638728	Sales return reversing group 1fa5d366-8f3f-42c6-9230-45a689bbf195 for invoice POS-20260729052638728	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-29 05:27:30.425167+00	00000000-0000-0000-0000-000000000001
f003e508-a843-45c3-bc05-5c5d17a4d660	1fa5d366-8f3f-42c6-9230-45a689bbf195	REVENUE	69800	0	REVERSAL	POS-20260729052638728	Sales return reversing group 1fa5d366-8f3f-42c6-9230-45a689bbf195 for invoice POS-20260729052638728	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-29 05:27:30.425167+00	00000000-0000-0000-0000-000000000001
807ab27f-0834-4113-80ac-391e5f931a2f	275511a4-e8a4-4b74-a572-e262f56bf79d	REVENUE	0	59800	SALE	POS-20260729052807378	Sale invoice POS-20260729052807378	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-29 05:28:07.399609+00	00000000-0000-0000-0000-000000000001
aca0d304-d968-4742-8943-4f01845be466	275511a4-e8a4-4b74-a572-e262f56bf79d	ACCOUNTS_RECEIVABLE	59800	0	SALE	POS-20260729052807378	Sale invoice POS-20260729052807378	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-29 05:28:07.399609+00	00000000-0000-0000-0000-000000000001
7e9c3efc-962f-480c-9cfd-33cf9d1f9fed	30dae30b-13fb-4cf0-9da7-f4cd96c7271c	REVENUE	0	41800	SALE	POS-20260729053301076	Sale invoice POS-20260729053301076	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-29 05:33:01.190387+00	00000000-0000-0000-0000-000000000001
b7bd615a-4b17-49ab-a59d-9907b6b3fbfb	30dae30b-13fb-4cf0-9da7-f4cd96c7271c	ACCOUNTS_RECEIVABLE	41800	0	SALE	POS-20260729053301076	Sale invoice POS-20260729053301076	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-29 05:33:01.190387+00	00000000-0000-0000-0000-000000000001
3fbf7df4-bebc-47e9-9127-1ac84492b983	30dae30b-13fb-4cf0-9da7-f4cd96c7271c	ACCOUNTS_RECEIVABLE	0	23800	REVERSAL	POS-20260729053301076	Sales return reversing group 30dae30b-13fb-4cf0-9da7-f4cd96c7271c for invoice POS-20260729053301076	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-29 05:34:26.478756+00	00000000-0000-0000-0000-000000000001
e4535388-cdd1-40a6-8fbd-00ce24e32c20	30dae30b-13fb-4cf0-9da7-f4cd96c7271c	REVENUE	23800	0	REVERSAL	POS-20260729053301076	Sales return reversing group 30dae30b-13fb-4cf0-9da7-f4cd96c7271c for invoice POS-20260729053301076	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-29 05:34:26.478756+00	00000000-0000-0000-0000-000000000001
032de9eb-1e7e-41aa-88b0-ed9ad09fece1	93d073d3-1e8e-4c01-b98f-9853028aeaf7	REVENUE	0	40000	SALE	POS-20260729053452286	Sale invoice POS-20260729053452286	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-29 05:34:52.299304+00	00000000-0000-0000-0000-000000000001
56da0856-a949-4506-acb0-2d1628c33fcb	93d073d3-1e8e-4c01-b98f-9853028aeaf7	ACCOUNTS_RECEIVABLE	40000	0	SALE	POS-20260729053452286	Sale invoice POS-20260729053452286	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-29 05:34:52.299304+00	00000000-0000-0000-0000-000000000001
37effc6f-a577-4096-b7f1-7d4effb74b94	93d073d3-1e8e-4c01-b98f-9853028aeaf7	ACCOUNTS_RECEIVABLE	0	40000	REVERSAL	POS-20260729053452286	Sales return reversing group 93d073d3-1e8e-4c01-b98f-9853028aeaf7 for invoice POS-20260729053452286	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-29 05:35:23.367043+00	00000000-0000-0000-0000-000000000001
8e4a1343-b029-4694-abcf-e2a2280bfd16	93d073d3-1e8e-4c01-b98f-9853028aeaf7	REVENUE	40000	0	REVERSAL	POS-20260729053452286	Sales return reversing group 93d073d3-1e8e-4c01-b98f-9853028aeaf7 for invoice POS-20260729053452286	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-29 05:35:23.367043+00	00000000-0000-0000-0000-000000000001
6a657f13-cb43-4cd6-9206-6e38e466c795	18488ac9-495a-4498-9149-5fa2ef07fa9e	ACCOUNTS_PAYABLE	0	22000	PURCHASE	83c4bec6-e557-4ebb-b319-359c9505abb2	Purchase receive invoice INV-20260729-1113	\N	00000000-0000-0000-0000-000000000301	2026-07-29 06:14:18.061183+00	00000000-0000-0000-0000-000000000001
dfd46c20-a0da-4494-947a-42959da5469d	18488ac9-495a-4498-9149-5fa2ef07fa9e	INVENTORY	22000	0	PURCHASE	83c4bec6-e557-4ebb-b319-359c9505abb2	Purchase receive invoice INV-20260729-1113	\N	00000000-0000-0000-0000-000000000301	2026-07-29 06:14:18.061183+00	00000000-0000-0000-0000-000000000001
34742ba3-7bfa-4e1b-a043-cf54d0884656	4eb25731-8b7b-492c-a9f5-2a850a8051c0	INVENTORY	180000	0	PURCHASE	8b344ca0-6121-4cdc-b7a4-4c392b92e29f	Purchase receive invoice INV-20260729-1114	\N	00000000-0000-0000-0000-000000000301	2026-07-29 06:19:42.206301+00	00000000-0000-0000-0000-000000000001
c0722100-cc49-45ec-84b0-975ab292e573	4eb25731-8b7b-492c-a9f5-2a850a8051c0	ACCOUNTS_PAYABLE	0	180000	PURCHASE	8b344ca0-6121-4cdc-b7a4-4c392b92e29f	Purchase receive invoice INV-20260729-1114	\N	00000000-0000-0000-0000-000000000301	2026-07-29 06:19:42.206301+00	00000000-0000-0000-0000-000000000001
5f1193b1-55eb-43e8-a5e9-971d146df0c4	4713c600-86b8-4f6b-a4b0-4b94bdf0681e	ACCOUNTS_PAYABLE	0	2840000	PURCHASE	1d841c12-802b-4250-82c8-28e807885f28	Purchase receive invoice INV-20260729-1120	\N	00000000-0000-0000-0000-000000000301	2026-07-29 06:28:39.944602+00	00000000-0000-0000-0000-000000000001
7fe2db29-85fc-4267-a52b-f5669353371e	4713c600-86b8-4f6b-a4b0-4b94bdf0681e	INVENTORY	2840000	0	PURCHASE	1d841c12-802b-4250-82c8-28e807885f28	Purchase receive invoice INV-20260729-1120	\N	00000000-0000-0000-0000-000000000301	2026-07-29 06:28:39.944602+00	00000000-0000-0000-0000-000000000001
1e4f4f15-cb4d-4881-92a3-1da0aafa418c	d06e678f-a37f-4408-ac9a-464b7046f0e4	CASH	40000	0	SALE	POS-20260729161859674	Sale invoice POS-20260729161859674	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	\N	2026-07-29 16:19:00.074217+00	00000000-0000-0000-0000-000000000001
b7e82248-c445-477f-966f-9e9592ced2c5	d06e678f-a37f-4408-ac9a-464b7046f0e4	REVENUE	0	40000	SALE	POS-20260729161859674	Sale invoice POS-20260729161859674	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	\N	2026-07-29 16:19:00.074217+00	00000000-0000-0000-0000-000000000001
71b08b33-1bfe-418c-8466-f42c59b629c2	f5f1c420-7ed3-406e-bc5e-68221deaf588	ACCOUNTS_RECEIVABLE	1800	0	SALE	POS-20260730131232419	Sale invoice POS-20260730131232419	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-30 13:12:33.080535+00	00000000-0000-0000-0000-000000000001
bcd1086e-f462-4b40-ac36-99c51c76d48a	f5f1c420-7ed3-406e-bc5e-68221deaf588	REVENUE	0	1800	SALE	POS-20260730131232419	Sale invoice POS-20260730131232419	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-30 13:12:33.080535+00	00000000-0000-0000-0000-000000000001
390d47d2-e522-4119-a20e-6ccca7a0a3d5	f5f1c420-7ed3-406e-bc5e-68221deaf588	REVENUE	1800	0	REVERSAL	POS-20260730131232419	Sales return reversing group f5f1c420-7ed3-406e-bc5e-68221deaf588 for invoice POS-20260730131232419	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-30 13:12:47.050896+00	00000000-0000-0000-0000-000000000001
9bbb986f-cc2f-4a84-ba95-7ee07e4427e1	f5f1c420-7ed3-406e-bc5e-68221deaf588	ACCOUNTS_RECEIVABLE	0	1800	REVERSAL	POS-20260730131232419	Sales return reversing group f5f1c420-7ed3-406e-bc5e-68221deaf588 for invoice POS-20260730131232419	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000501	2026-07-30 13:12:47.050896+00	00000000-0000-0000-0000-000000000001
152cc592-947c-4004-97e1-71257166bd87	d06e678f-a37f-4408-ac9a-464b7046f0e4	CASH	0	40000	REVERSAL	POS-20260729161859674	Sales return reversing group d06e678f-a37f-4408-ac9a-464b7046f0e4 for invoice POS-20260729161859674	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	\N	2026-07-30 13:12:59.850347+00	00000000-0000-0000-0000-000000000001
f145e1a2-c299-4b65-9fda-b79161c844b6	d06e678f-a37f-4408-ac9a-464b7046f0e4	REVENUE	40000	0	REVERSAL	POS-20260729161859674	Sales return reversing group d06e678f-a37f-4408-ac9a-464b7046f0e4 for invoice POS-20260729161859674	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	\N	2026-07-30 13:12:59.850347+00	00000000-0000-0000-0000-000000000001
7b946d40-2d70-4370-8690-711db24f49da	f303b5eb-cab1-4f60-9d54-cdd168402b3b	INVENTORY	7000	0	PURCHASE	91785450-5722-421f-8acb-ea6157a31c6d	Purchase receive invoice 090-99	\N	9a3db5ed-d7d5-4241-8db9-802d70bddb84	2026-08-01 16:59:35.499864+00	00000000-0000-0000-0000-000000000001
c227464e-29ce-48b9-8ba7-23d9b5615865	f303b5eb-cab1-4f60-9d54-cdd168402b3b	ACCOUNTS_PAYABLE	0	7000	PURCHASE	91785450-5722-421f-8acb-ea6157a31c6d	Purchase receive invoice 090-99	\N	9a3db5ed-d7d5-4241-8db9-802d70bddb84	2026-08-01 16:59:35.499864+00	00000000-0000-0000-0000-000000000001
263f1a89-957b-437a-a65d-deda7a3d34ba	a879e966-b028-48cd-b8f5-e075563fd094	ACCOUNTS_PAYABLE	0	462000	PURCHASE	8b95d73b-f532-48ee-9864-66f144713ef0	Purchase receive invoice INV-20260801-2155	\N	00000000-0000-0000-0000-000000000301	2026-08-01 17:00:08.838093+00	00000000-0000-0000-0000-000000000001
2999212f-9a42-4b64-90d9-08d0486e1fd4	a879e966-b028-48cd-b8f5-e075563fd094	INVENTORY	462000	0	PURCHASE	8b95d73b-f532-48ee-9864-66f144713ef0	Purchase receive invoice INV-20260801-2155	\N	00000000-0000-0000-0000-000000000301	2026-08-01 17:00:08.838093+00	00000000-0000-0000-0000-000000000001
2fd34126-ab0d-48e4-8c19-3d9148307a37	a9020dfa-69b3-4f89-b0e6-cb0072aaddff	EXPENSE:MAINTENANCE	90000	0	EXPENSE	EXP-20260801-170719-7215	001	\N	\N	2026-08-01 17:07:19.635373+00	00000000-0000-0000-0000-000000000001
6f0e7b7d-34cb-4b71-bfb3-f8f4312b1a97	a9020dfa-69b3-4f89-b0e6-cb0072aaddff	CASH	0	90000	EXPENSE	EXP-20260801-170719-7215	001	\N	\N	2026-08-01 17:07:19.635373+00	00000000-0000-0000-0000-000000000001
33879cf2-b62e-4e3b-b306-8e9b473a401c	261aa966-8ccf-42d7-af3a-c7fb979c99e9	REVENUE	0	10000	SALE	POS-20260801174910855	Sale invoice POS-20260801174910855	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	\N	2026-08-01 17:49:11.422092+00	00000000-0000-0000-0000-000000000001
d5789537-4687-45a5-9f0c-e370ab429446	261aa966-8ccf-42d7-af3a-c7fb979c99e9	CASH	10000	0	SALE	POS-20260801174910855	Sale invoice POS-20260801174910855	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	\N	2026-08-01 17:49:11.422092+00	00000000-0000-0000-0000-000000000001
a97acec1-be33-4e0f-be58-43c489da1b14	6b6336b4-adba-427e-8686-426f9055be53	INVENTORY	1000000	0	PURCHASE	34819be3-8196-4ded-8b66-fe9c43422e9d	Purchase receive invoice INV-20260801-2249	\N	9a3db5ed-d7d5-4241-8db9-802d70bddb84	2026-08-01 17:49:42.879355+00	00000000-0000-0000-0000-000000000001
c815cfe6-78d2-46aa-8a84-28ef0a39b19d	6b6336b4-adba-427e-8686-426f9055be53	ACCOUNTS_PAYABLE	0	1000000	PURCHASE	34819be3-8196-4ded-8b66-fe9c43422e9d	Purchase receive invoice INV-20260801-2249	\N	9a3db5ed-d7d5-4241-8db9-802d70bddb84	2026-08-01 17:49:42.879355+00	00000000-0000-0000-0000-000000000001
4fe942d7-d605-4d65-9b3f-7d57da545f0d	42c240cc-2d14-41b4-a604-eb2c6427f046	REVENUE	0	130000	SALE	POS-20260801175024761	Sale invoice POS-20260801175024761	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	\N	2026-08-01 17:50:24.837169+00	00000000-0000-0000-0000-000000000001
6517ff8e-a00d-4d54-a807-568ea04fec7c	42c240cc-2d14-41b4-a604-eb2c6427f046	CASH	130000	0	SALE	POS-20260801175024761	Sale invoice POS-20260801175024761	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	\N	2026-08-01 17:50:24.837169+00	00000000-0000-0000-0000-000000000001
842731e0-ef77-4e9f-8744-b3c856d73060	c6cc93c2-73f1-410d-bfd6-5bdfce3e9a58	ACCOUNTS_PAYABLE	0	700000	PURCHASE	de18dbde-621f-404e-b446-7782d8865d72	Purchase receive invoice 098	\N	9a3db5ed-d7d5-4241-8db9-802d70bddb84	2026-08-01 18:27:44.837997+00	00000000-0000-0000-0000-000000000001
cd1eb7dd-0acc-4236-a092-0fe997555b1c	c6cc93c2-73f1-410d-bfd6-5bdfce3e9a58	INVENTORY	700000	0	PURCHASE	de18dbde-621f-404e-b446-7782d8865d72	Purchase receive invoice 098	\N	9a3db5ed-d7d5-4241-8db9-802d70bddb84	2026-08-01 18:27:44.837997+00	00000000-0000-0000-0000-000000000001
01145721-8266-497d-bb63-dd5dafc65f1d	9e991137-81c9-42b6-baee-5851a6d19f33	ACCOUNTS_PAYABLE	0	10000	PURCHASE	f693a63a-5dc5-4d30-b964-0bc0e760e3be	Purchase receive invoice INV-20260801-2332	\N	4e8ea130-d6dc-4ef5-b5df-3ffcff0001c0	2026-08-01 18:33:07.258107+00	00000000-0000-0000-0000-000000000001
4f7ca026-f50e-49ef-86c6-144aa0d3ef78	9e991137-81c9-42b6-baee-5851a6d19f33	INVENTORY	10000	0	PURCHASE	f693a63a-5dc5-4d30-b964-0bc0e760e3be	Purchase receive invoice INV-20260801-2332	\N	4e8ea130-d6dc-4ef5-b5df-3ffcff0001c0	2026-08-01 18:33:07.258107+00	00000000-0000-0000-0000-000000000001
ab03bb62-bb5e-4cf5-9ae9-54ee78a8532a	4df41bc1-d4f8-40fa-8bce-a588dbe8284b	INVENTORY	1500000	0	PURCHASE	1ad0a64a-70be-475a-9795-d8b86129f06c	Purchase receive invoice 10345	\N	4e8ea130-d6dc-4ef5-b5df-3ffcff0001c0	2026-08-01 18:34:19.574685+00	00000000-0000-0000-0000-000000000001
d43fe604-2291-4787-a803-c7f03816af45	4df41bc1-d4f8-40fa-8bce-a588dbe8284b	ACCOUNTS_PAYABLE	0	1500000	PURCHASE	1ad0a64a-70be-475a-9795-d8b86129f06c	Purchase receive invoice 10345	\N	4e8ea130-d6dc-4ef5-b5df-3ffcff0001c0	2026-08-01 18:34:19.574685+00	00000000-0000-0000-0000-000000000001
0c4d662d-ff66-427b-882e-b9c89b21a895	56c56a1a-288e-44a0-b92a-788cbfad63b1	REVENUE	0	15000	SALE	POS-20260801183518927	Sale invoice POS-20260801183518927	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	\N	2026-08-01 18:35:19.174053+00	00000000-0000-0000-0000-000000000001
7c08a0a3-e882-4fbd-b34c-973b575e04e0	56c56a1a-288e-44a0-b92a-788cbfad63b1	CASH	15000	0	SALE	POS-20260801183518927	Sale invoice POS-20260801183518927	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	\N	2026-08-01 18:35:19.174053+00	00000000-0000-0000-0000-000000000001
5229aa9a-2a2f-474d-ae31-86a792e435d2	bf331f3c-5b7c-4bcd-bee5-6b8fe78f3c2c	ACCOUNTS_RECEIVABLE	65000	0	SALE	POS-20260801202203648	Sale invoice POS-20260801202203648	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	2cd5a00a-3c63-4b20-bbab-8e225c89a1ed	2026-08-01 20:22:04.248013+00	00000000-0000-0000-0000-000000000001
cca7d366-36f6-43c2-9dee-b2ed60cb9521	bf331f3c-5b7c-4bcd-bee5-6b8fe78f3c2c	REVENUE	0	65000	SALE	POS-20260801202203648	Sale invoice POS-20260801202203648	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	2cd5a00a-3c63-4b20-bbab-8e225c89a1ed	2026-08-01 20:22:04.248013+00	00000000-0000-0000-0000-000000000001
07f07d09-8907-43fa-9a83-436cbcfbb1ae	6e9145ac-24bf-4546-b053-af08ad9d95d8	ACCOUNTS_RECEIVABLE	93000	0	SALE	POS-20260801202230688	Sale invoice POS-20260801202230688	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	2cd5a00a-3c63-4b20-bbab-8e225c89a1ed	2026-08-01 20:22:30.782776+00	00000000-0000-0000-0000-000000000001
0a89adf2-8fc7-46ed-82d1-6da2fcdd5644	6e9145ac-24bf-4546-b053-af08ad9d95d8	REVENUE	0	93000	SALE	POS-20260801202230688	Sale invoice POS-20260801202230688	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	2cd5a00a-3c63-4b20-bbab-8e225c89a1ed	2026-08-01 20:22:30.782776+00	00000000-0000-0000-0000-000000000001
74b9807f-d679-4639-a852-fc700fa373e8	6669897c-ce69-4d86-81f6-24f551711625	JAZZCASH	43000	0	SALE	POS-20260801202416953	Sale invoice POS-20260801202416953	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	2cd5a00a-3c63-4b20-bbab-8e225c89a1ed	2026-08-01 20:24:17.020213+00	00000000-0000-0000-0000-000000000001
b51f69fe-cb76-41ef-a4b7-b69561d894cb	6669897c-ce69-4d86-81f6-24f551711625	REVENUE	0	43000	SALE	POS-20260801202416953	Sale invoice POS-20260801202416953	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	2cd5a00a-3c63-4b20-bbab-8e225c89a1ed	2026-08-01 20:24:17.020213+00	00000000-0000-0000-0000-000000000001
886911b8-91c8-4220-9a5b-73e4d3e2e7b8	d8d6e888-1d88-4641-9469-6ab616ae2393	REVENUE	0	43000	SALE	POS-20260801203114999	Sale invoice POS-20260801203114999	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	2cd5a00a-3c63-4b20-bbab-8e225c89a1ed	2026-08-01 20:31:15.108057+00	00000000-0000-0000-0000-000000000001
b5e9f310-f002-4fa4-9c6c-e2c956979eec	d8d6e888-1d88-4641-9469-6ab616ae2393	ACCOUNTS_RECEIVABLE	43000	0	SALE	POS-20260801203114999	Sale invoice POS-20260801203114999	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	2cd5a00a-3c63-4b20-bbab-8e225c89a1ed	2026-08-01 20:31:15.108057+00	00000000-0000-0000-0000-000000000001
0536557d-446f-4022-8d39-e181db1ec176	d8d6e888-1d88-4641-9469-6ab616ae2393	REVENUE	43000	0	REVERSAL	POS-20260801203114999	Sales return reversing group d8d6e888-1d88-4641-9469-6ab616ae2393 for invoice POS-20260801203114999	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	2cd5a00a-3c63-4b20-bbab-8e225c89a1ed	2026-08-01 20:31:34.976668+00	00000000-0000-0000-0000-000000000001
6335d998-96ff-4d99-b9bb-490915fdfc30	d8d6e888-1d88-4641-9469-6ab616ae2393	ACCOUNTS_RECEIVABLE	0	43000	REVERSAL	POS-20260801203114999	Sales return reversing group d8d6e888-1d88-4641-9469-6ab616ae2393 for invoice POS-20260801203114999	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	2cd5a00a-3c63-4b20-bbab-8e225c89a1ed	2026-08-01 20:31:34.976668+00	00000000-0000-0000-0000-000000000001
345b192f-ba79-41e4-9cf9-c68ce29c29f7	68f51d9a-754a-4847-8b33-9f71dd1ca5d5	ACCOUNTS_PAYABLE	1500000	0	PARTY_ADJUSTMENT	ADJ-101 — Adjustment	101 — Adjustment	\N	00000000-0000-0000-0000-000000000301	2026-08-02 07:21:27.619317+00	00000000-0000-0000-0000-000000000001
ccc01a09-0977-4394-875b-28a851a510db	68f51d9a-754a-4847-8b33-9f71dd1ca5d5	EXPENSE:OTHER	0	1500000	PARTY_ADJUSTMENT	ADJ-101 — Adjustment	101 — Adjustment	\N	00000000-0000-0000-0000-000000000301	2026-08-02 07:21:27.619317+00	00000000-0000-0000-0000-000000000001
0036786b-a1c5-47c9-92da-17c739e032fe	4ca64a6f-03d1-44d9-8ba0-4c8ae59275b6	ACCOUNTS_RECEIVABLE	0	300000	CUSTOMER_PAYMENT	PAY-20260802142336	Customer payment PAY-20260802142336	b61e76a2-11dc-46ab-8858-767e63e627e9	00000000-0000-0000-0000-000000000501	2026-08-02 14:23:36.229225+00	00000000-0000-0000-0000-000000000001
a85fa08b-cb49-4a3c-bc8a-0d813a2bc711	4ca64a6f-03d1-44d9-8ba0-4c8ae59275b6	CASH	300000	0	CUSTOMER_PAYMENT	PAY-20260802142336	Customer payment PAY-20260802142336	b61e76a2-11dc-46ab-8858-767e63e627e9	00000000-0000-0000-0000-000000000501	2026-08-02 14:23:36.229225+00	00000000-0000-0000-0000-000000000001
6d3373d2-da86-4f0c-af67-6f14d742447e	9cd53534-dbcc-46de-a320-83b3eb95e1cb	CASH	0	1000000	SUPPLIER_PAYMENT	PAY-20260802142505	Supplier payment PAY-20260802142505	b61e76a2-11dc-46ab-8858-767e63e627e9	9a3db5ed-d7d5-4241-8db9-802d70bddb84	2026-08-02 14:25:05.087453+00	00000000-0000-0000-0000-000000000001
9a938a40-e895-437a-8a88-826ae7613e86	9cd53534-dbcc-46de-a320-83b3eb95e1cb	ACCOUNTS_PAYABLE	1000000	0	SUPPLIER_PAYMENT	PAY-20260802142505	Supplier payment PAY-20260802142505	b61e76a2-11dc-46ab-8858-767e63e627e9	9a3db5ed-d7d5-4241-8db9-802d70bddb84	2026-08-02 14:25:05.087453+00	00000000-0000-0000-0000-000000000001
67487394-4b91-4f15-abaa-e19481005daf	3a731e3c-778e-4440-bf1c-feb6e83c0c94	ACCOUNTS_PAYABLE	300000	0	SUPPLIER_PAYMENT	PAY-20260802142551	Supplier payment PAY-20260802142551	b61e76a2-11dc-46ab-8858-767e63e627e9	4e8ea130-d6dc-4ef5-b5df-3ffcff0001c0	2026-08-02 14:25:51.293537+00	00000000-0000-0000-0000-000000000001
88d5ac22-5152-42ba-93be-357c56e8ff01	3a731e3c-778e-4440-bf1c-feb6e83c0c94	CASH	0	300000	SUPPLIER_PAYMENT	PAY-20260802142551	Supplier payment PAY-20260802142551	b61e76a2-11dc-46ab-8858-767e63e627e9	4e8ea130-d6dc-4ef5-b5df-3ffcff0001c0	2026-08-02 14:25:51.293537+00	00000000-0000-0000-0000-000000000001
\.


--
-- Data for Name: parties; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.parties (id, party_type, name, phone_number, address, credit_limit_paisa, current_balance_paisa, created_at, updated_at, tenant_id, is_deleted) FROM stdin;
2cd5a00a-3c63-4b20-bbab-8e225c89a1ed	CUSTOMER	Ammar	03143219489	zumurd Town chakwal	0	158000	2026-07-30 13:55:48.697602+00	2026-07-30 13:55:48.697602+00	00000000-0000-0000-0000-000000000001	f
00000000-0000-0000-0000-000000000301	SUPPLIER	Pilot Dairy Farm	03001234567	Pilot Farm	0	7744000	2026-07-25 11:13:08.767357+00	2026-07-25 11:13:08.767357+00	00000000-0000-0000-0000-000000000001	f
00000000-0000-0000-0000-000000000501	CUSTOMER	Copenhagen Mart Regular	03009998877	Copenhagen Mart	500000	-137250	2026-07-25 11:13:08.767357+00	2026-07-25 11:13:08.767357+00	00000000-0000-0000-0000-000000000001	f
9a3db5ed-d7d5-4241-8db9-802d70bddb84	SUPPLIER	Lays	333333	chakwal	0	707000	2026-07-30 15:33:27.829533+00	2026-07-30 15:33:27.829533+00	00000000-0000-0000-0000-000000000001	f
4e8ea130-d6dc-4ef5-b5df-3ffcff0001c0	SUPPLIER	Walls	222222	chak	0	1210000	2026-07-30 15:33:12.802243+00	2026-07-30 15:33:12.802243+00	00000000-0000-0000-0000-000000000001	f
\.


--
-- Data for Name: party_ledgers; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.party_ledgers (id, party_id, invoice_no, purchase_order_id, transaction_type, payment_channel, old_balance_paisa, amount_paisa, running_balance_paisa, reference_number, created_at, tenant_id, credit_paisa, debit_paisa) FROM stdin;
3220f8fa-ac95-4923-ae62-97fa597ed0d6	00000000-0000-0000-0000-000000000301	\N	38368f63-9951-41a4-9f62-1afa8b222360	PURCHASE	CREDIT	0	3200000	3200000	38368f63-9951-41a4-9f62-1afa8b222360	2026-07-25 12:43:37.523078+00	00000000-0000-0000-0000-000000000001	0	3200000
5310ac76-a76d-4597-87d7-19cba13d1001	00000000-0000-0000-0000-000000000301	\N	36295608-81aa-49bd-85fe-d9debebab6c1	PURCHASE	CREDIT	3200000	180000	3380000	36295608-81aa-49bd-85fe-d9debebab6c1	2026-07-25 12:44:42.216738+00	00000000-0000-0000-0000-000000000001	0	180000
7449037a-cf10-4603-b2d6-0469fdc0f462	00000000-0000-0000-0000-000000000501	POS-20260726101308608	\N	SALE	CASH	0	19800	0	POS-20260726101308608	2026-07-26 10:13:08.659675+00	00000000-0000-0000-0000-000000000001	0	0
66f38796-75d9-4367-85a7-e6d7be5d27dc	00000000-0000-0000-0000-000000000301	\N	aa51afad-b03d-462f-8aae-a5a849fd9429	PURCHASE	CREDIT	3380000	2360000	5740000	aa51afad-b03d-462f-8aae-a5a849fd9429	2026-07-26 10:15:17.516806+00	00000000-0000-0000-0000-000000000001	0	2360000
1338e30d-99ff-4979-bb88-822e2e961f27	00000000-0000-0000-0000-000000000501	POS-20260728093244680	\N	SALE	CASH	0	69800	0	POS-20260728093244680	2026-07-28 09:32:45.236679+00	00000000-0000-0000-0000-000000000001	0	0
55eedfe3-a5df-4d66-8638-8beaac58e9b5	00000000-0000-0000-0000-000000000501	POS-20260728093434699	\N	SALE	CREDIT	0	19800	19800	POS-20260728093434699	2026-07-28 09:34:34.749421+00	00000000-0000-0000-0000-000000000001	0	19800
6b71a8ee-3a67-4ee7-8a5e-d817ffb66cf4	00000000-0000-0000-0000-000000000501	POS-20260728093503740	\N	SALE	CREDIT	19800	41800	61600	POS-20260728093503740	2026-07-28 09:35:03.758028+00	00000000-0000-0000-0000-000000000001	0	41800
f3362f2a-c48b-4886-96e9-c0e176f96b5f	00000000-0000-0000-0000-000000000501	POS-20260728093503740	\N	CUSTOMER_RETURN	CREDIT	61600	41800	19800	POS-20260728093503740	2026-07-28 09:35:46.884598+00	00000000-0000-0000-0000-000000000001	41800	0
f631e260-35c5-461d-89ad-6059908f989a	00000000-0000-0000-0000-000000000501	POS-20260728093434699	\N	CUSTOMER_RETURN	CREDIT	19800	18450	1350	POS-20260728093434699	2026-07-28 16:02:02.564346+00	00000000-0000-0000-0000-000000000001	18450	0
453dd716-1f24-4e3e-9b4e-d164f3fc553f	00000000-0000-0000-0000-000000000501	POS-20260728162629268	\N	SALE	CREDIT	1350	41800	43150	POS-20260728162629268	2026-07-28 16:26:29.373662+00	00000000-0000-0000-0000-000000000001	0	41800
0433d135-4bc2-481b-9d42-26e638854cf3	00000000-0000-0000-0000-000000000501	POS-20260729050728574	\N	SALE	CREDIT	43150	40000	83150	POS-20260729050728574	2026-07-29 05:07:28.615641+00	00000000-0000-0000-0000-000000000001	0	40000
a2c17daf-b9b4-49cc-8e03-82b423e77f27	00000000-0000-0000-0000-000000000501	POS-20260729050811071	\N	SALE	JAZZCASH	83150	40000	83150	POS-20260729050811071	2026-07-29 05:08:11.092027+00	00000000-0000-0000-0000-000000000001	0	0
ebcf1ebf-5a2b-4427-ba22-75e18e64ddfe	00000000-0000-0000-0000-000000000501	POS-20260729052558867	\N	SALE	CREDIT	83150	1800	84950	POS-20260729052558867	2026-07-29 05:25:58.919631+00	00000000-0000-0000-0000-000000000001	0	1800
0fecc3fd-2e47-48a5-a886-4ebf53020afc	00000000-0000-0000-0000-000000000501	POS-20260729052638728	\N	SALE	CREDIT	84950	69800	154750	POS-20260729052638728	2026-07-29 05:26:38.756517+00	00000000-0000-0000-0000-000000000001	0	69800
320b3817-28b3-4ae3-bd15-ab817800c4a0	00000000-0000-0000-0000-000000000501	POS-20260729052638728	\N	CUSTOMER_RETURN	CREDIT	154750	69800	84950	POS-20260729052638728	2026-07-29 05:27:30.432547+00	00000000-0000-0000-0000-000000000001	69800	0
92d95bdc-916d-4ca1-b9df-77aad096cc1b	00000000-0000-0000-0000-000000000501	POS-20260729052807378	\N	SALE	CREDIT	84950	59800	144750	POS-20260729052807378	2026-07-29 05:28:07.401675+00	00000000-0000-0000-0000-000000000001	0	59800
c165779d-2d37-4739-8d98-31f92dc278c6	00000000-0000-0000-0000-000000000501	POS-20260729053301076	\N	SALE	CREDIT	144750	41800	186550	POS-20260729053301076	2026-07-29 05:33:01.193601+00	00000000-0000-0000-0000-000000000001	0	41800
f9b86f01-e3fe-478a-85c7-af8b96ea07b1	00000000-0000-0000-0000-000000000501	POS-20260729053301076	\N	CUSTOMER_RETURN	CREDIT	186550	23800	162750	POS-20260729053301076	2026-07-29 05:34:26.481639+00	00000000-0000-0000-0000-000000000001	23800	0
a7d34f2d-5e05-4237-926c-f11b14bb1e87	00000000-0000-0000-0000-000000000501	POS-20260729053452286	\N	SALE	CREDIT	162750	40000	202750	POS-20260729053452286	2026-07-29 05:34:52.301095+00	00000000-0000-0000-0000-000000000001	0	40000
bc07a4f2-5f16-4030-a0c6-603f506e48e9	00000000-0000-0000-0000-000000000501	POS-20260729053452286	\N	CUSTOMER_RETURN	CREDIT	202750	40000	162750	POS-20260729053452286	2026-07-29 05:35:23.370871+00	00000000-0000-0000-0000-000000000001	40000	0
6e875d7d-9c42-4ce6-8586-49062be2db8e	00000000-0000-0000-0000-000000000301	\N	83c4bec6-e557-4ebb-b319-359c9505abb2	PURCHASE	CREDIT	5740000	22000	5762000	83c4bec6-e557-4ebb-b319-359c9505abb2	2026-07-29 06:14:18.144005+00	00000000-0000-0000-0000-000000000001	0	22000
52ed437c-acbb-4ba7-bda1-8edefafbbaff	00000000-0000-0000-0000-000000000301	\N	8b344ca0-6121-4cdc-b7a4-4c392b92e29f	PURCHASE	CREDIT	5762000	180000	5942000	8b344ca0-6121-4cdc-b7a4-4c392b92e29f	2026-07-29 06:19:42.209243+00	00000000-0000-0000-0000-000000000001	0	180000
4b1e5099-c289-4d1a-833b-98e32cd4a965	00000000-0000-0000-0000-000000000301	\N	1d841c12-802b-4250-82c8-28e807885f28	PURCHASE	CREDIT	5942000	2840000	8782000	1d841c12-802b-4250-82c8-28e807885f28	2026-07-29 06:28:39.947216+00	00000000-0000-0000-0000-000000000001	0	2840000
ca069a8d-0544-4fdc-ade2-84fcb0c46e24	00000000-0000-0000-0000-000000000501	POS-20260730131232419	\N	SALE	CREDIT	162750	1800	164550	POS-20260730131232419	2026-07-30 13:12:33.122723+00	00000000-0000-0000-0000-000000000001	0	1800
b16d5ffd-d0ed-406f-9f92-2ad544a2be8f	00000000-0000-0000-0000-000000000501	POS-20260730131232419	\N	CUSTOMER_RETURN	CREDIT	164550	1800	162750	POS-20260730131232419	2026-07-30 13:12:47.057761+00	00000000-0000-0000-0000-000000000001	1800	0
14881cd0-a01a-4e4d-8dd7-00e4481d1a82	4e8ea130-d6dc-4ef5-b5df-3ffcff0001c0	\N	\N	OPENING	CREDIT	0	0	0	SUPPLIER_INIT	2026-07-30 15:33:12.866512+00	00000000-0000-0000-0000-000000000001	0	0
ffed55e6-e226-4f80-9c7b-4821ffc054c8	9a3db5ed-d7d5-4241-8db9-802d70bddb84	\N	\N	OPENING	CREDIT	0	0	0	SUPPLIER_INIT	2026-07-30 15:33:27.849984+00	00000000-0000-0000-0000-000000000001	0	0
11df5e10-de0c-4fa7-9a47-e149df660b21	9a3db5ed-d7d5-4241-8db9-802d70bddb84	\N	91785450-5722-421f-8acb-ea6157a31c6d	PURCHASE	CREDIT	0	7000	7000	91785450-5722-421f-8acb-ea6157a31c6d	2026-08-01 16:59:35.583568+00	00000000-0000-0000-0000-000000000001	0	7000
4dd4d46b-cc98-4d41-95dc-44ed42d9813c	00000000-0000-0000-0000-000000000301	\N	8b95d73b-f532-48ee-9864-66f144713ef0	PURCHASE	CREDIT	8782000	462000	9244000	8b95d73b-f532-48ee-9864-66f144713ef0	2026-08-01 17:00:08.83994+00	00000000-0000-0000-0000-000000000001	0	462000
083bd2b0-8e6d-49d5-b7ad-fe38e8dfc32c	9a3db5ed-d7d5-4241-8db9-802d70bddb84	\N	34819be3-8196-4ded-8b66-fe9c43422e9d	PURCHASE	CREDIT	7000	1000000	1007000	34819be3-8196-4ded-8b66-fe9c43422e9d	2026-08-01 17:49:42.884075+00	00000000-0000-0000-0000-000000000001	0	1000000
848093b0-9845-47a4-9496-4eeaa5a30e1a	9a3db5ed-d7d5-4241-8db9-802d70bddb84	\N	de18dbde-621f-404e-b446-7782d8865d72	PURCHASE	CREDIT	1007000	700000	1707000	de18dbde-621f-404e-b446-7782d8865d72	2026-08-01 18:27:44.944741+00	00000000-0000-0000-0000-000000000001	0	700000
13d717aa-8f84-4919-aba6-8489ddc10977	4e8ea130-d6dc-4ef5-b5df-3ffcff0001c0	\N	f693a63a-5dc5-4d30-b964-0bc0e760e3be	PURCHASE	CREDIT	0	10000	10000	f693a63a-5dc5-4d30-b964-0bc0e760e3be	2026-08-01 18:33:07.260043+00	00000000-0000-0000-0000-000000000001	0	10000
98a7a4d1-2334-41af-a700-15b30469c0d8	4e8ea130-d6dc-4ef5-b5df-3ffcff0001c0	\N	1ad0a64a-70be-475a-9795-d8b86129f06c	PURCHASE	CREDIT	10000	1500000	1510000	1ad0a64a-70be-475a-9795-d8b86129f06c	2026-08-01 18:34:19.578731+00	00000000-0000-0000-0000-000000000001	0	1500000
317725b7-7122-47f3-a7f7-764455477474	2cd5a00a-3c63-4b20-bbab-8e225c89a1ed	POS-20260801202203648	\N	SALE	CREDIT	0	65000	65000	POS-20260801202203648	2026-08-01 20:22:04.257596+00	00000000-0000-0000-0000-000000000001	0	65000
08bd7964-27ef-43de-8491-22b4062d7b51	2cd5a00a-3c63-4b20-bbab-8e225c89a1ed	POS-20260801202230688	\N	SALE	CREDIT	65000	93000	158000	POS-20260801202230688	2026-08-01 20:22:30.785608+00	00000000-0000-0000-0000-000000000001	0	93000
cffc1034-d5c1-41c7-b3a9-b9dda88c0c9d	2cd5a00a-3c63-4b20-bbab-8e225c89a1ed	POS-20260801202416953	\N	SALE	JAZZCASH	158000	43000	158000	POS-20260801202416953	2026-08-01 20:24:17.029128+00	00000000-0000-0000-0000-000000000001	0	0
d4537ea5-fddb-4b22-906c-a35cb7ffb96c	2cd5a00a-3c63-4b20-bbab-8e225c89a1ed	POS-20260801203114999	\N	SALE	CREDIT	158000	43000	201000	POS-20260801203114999	2026-08-01 20:31:15.110369+00	00000000-0000-0000-0000-000000000001	0	43000
23995bc7-15f0-4a21-839f-b1d66e708864	2cd5a00a-3c63-4b20-bbab-8e225c89a1ed	POS-20260801203114999	\N	CUSTOMER_RETURN	CREDIT	201000	43000	158000	POS-20260801203114999	2026-08-01 20:31:34.980818+00	00000000-0000-0000-0000-000000000001	43000	0
8aa9a309-6f3d-4d04-9191-695c7b720b79	00000000-0000-0000-0000-000000000301	\N	\N	ADJUSTMENT	CREDIT	9244000	1500000	7744000	ADJ-101 — Adjustment	2026-08-02 07:21:27.707227+00	00000000-0000-0000-0000-000000000001	1500000	0
cb675d83-dc39-45c1-92ac-e30c710eff2c	00000000-0000-0000-0000-000000000501	\N	\N	PAYMENT	CASH	162750	300000	-137250	PAY-20260802142336	2026-08-02 14:23:36.251649+00	00000000-0000-0000-0000-000000000001	300000	0
5420dcd3-0a8c-4584-ba01-de0c7280dbdd	9a3db5ed-d7d5-4241-8db9-802d70bddb84	\N	\N	PAYMENT	CASH	1707000	1000000	707000	PAY-20260802142505	2026-08-02 14:25:05.090242+00	00000000-0000-0000-0000-000000000001	1000000	0
2ecdbce9-ed77-43b0-bc1c-e4ab3eac6270	4e8ea130-d6dc-4ef5-b5df-3ffcff0001c0	\N	\N	PAYMENT	CASH	1510000	300000	1210000	PAY-20260802142551	2026-08-02 14:25:51.29751+00	00000000-0000-0000-0000-000000000001	300000	0
\.


--
-- Data for Name: party_payment_allocations; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.party_payment_allocations (id, party_ledger_id, invoice_no, purchase_order_id, amount_paisa, created_at, tenant_id) FROM stdin;
\.


--
-- Data for Name: product_batches; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.product_batches (id, product_id, batch_number, expiry_date, cost_price_paisa, retail_price_paisa, initial_qty, current_qty, supplier_id, rack_location, purchase_order_id, created_at, tenant_id) FROM stdin;
42bb22b9-7197-4b56-954d-1f2edfece4c5	00000000-0000-0000-0000-000000000101	BATCH-260801	\N	22000	22000	21.000	21.000	00000000-0000-0000-0000-000000000301	\N	8b95d73b-f532-48ee-9864-66f144713ef0	2026-08-01 17:00:08.837591+00	00000000-0000-0000-0000-000000000001
1e44615d-837b-40be-bc71-9ba7d17ab80e	afd93f49-d720-4a71-bfb7-629c3b0fddf6	PO-090-99-afd93f49d7204a71bfb7629c3b0fddf6	\N	7000	10000	1.000	0.000	9a3db5ed-d7d5-4241-8db9-802d70bddb84	\N	91785450-5722-421f-8acb-ea6157a31c6d	2026-08-01 16:59:35.391609+00	00000000-0000-0000-0000-000000000001
00000000-0000-0000-0000-000000000201	00000000-0000-0000-0000-000000000101	BUF-PILOT-01	2026-07-27	18000	22000	100.000	98.000	00000000-0000-0000-0000-000000000301	COLD-1	\N	2026-07-25 11:13:08.767357+00	00000000-0000-0000-0000-000000000001
00000000-0000-0000-0000-000000000203	00000000-0000-0000-0000-000000000103	BAN-PILOT-01	2026-07-27	22000	28000	50.000	47.000	00000000-0000-0000-0000-000000000301	PRODUCE-1	\N	2026-07-25 11:13:08.767357+00	00000000-0000-0000-0000-000000000001
00000000-0000-0000-0000-000000000202	00000000-0000-0000-0000-000000000102	COW-PILOT-01	2026-07-27	14000	18000	100.000	96.000	00000000-0000-0000-0000-000000000301	COLD-1	\N	2026-07-25 11:13:08.767357+00	00000000-0000-0000-0000-000000000001
00000000-0000-0000-0000-000000000204	00000000-0000-0000-0000-000000000104	SAM-PILOT-01	2026-07-27	1200	1800	200.000	197.000	00000000-0000-0000-0000-000000000301	BAKERY-1	\N	2026-07-25 11:13:08.767357+00	00000000-0000-0000-0000-000000000001
04ca6dea-04c8-4d79-a369-2f174456e6f4	00000000-0000-0000-0000-000000000103	BATCH-260726	\N	28000	28000	20.000	20.000	00000000-0000-0000-0000-000000000301	\N	aa51afad-b03d-462f-8aae-a5a849fd9429	2026-07-26 10:15:17.51328+00	00000000-0000-0000-0000-000000000001
2c12cb47-dff3-41ae-a5de-9ed724fd0655	afd93f49-d720-4a71-bfb7-629c3b0fddf6	BATCH-260801	\N	10000	10000	100.000	87.000	9a3db5ed-d7d5-4241-8db9-802d70bddb84	\N	34819be3-8196-4ded-8b66-fe9c43422e9d	2026-08-01 17:49:42.87766+00	00000000-0000-0000-0000-000000000001
28939ee2-d067-4516-8f68-e1401dc2d9de	afd93f49-d720-4a71-bfb7-629c3b0fddf6	PO-098-afd93f49d7204a71bfb7629c3b0fddf6	\N	7000	12000	100.000	100.000	9a3db5ed-d7d5-4241-8db9-802d70bddb84	\N	de18dbde-621f-404e-b446-7782d8865d72	2026-08-01 18:27:44.726434+00	00000000-0000-0000-0000-000000000001
34abfea8-c687-4586-8da1-a112a57a5712	c3891d5e-a5ea-472a-9ba1-80b59266207a	BATCH-260801	\N	10000	15000	1.000	0.000	4e8ea130-d6dc-4ef5-b5df-3ffcff0001c0	\N	f693a63a-5dc5-4d30-b964-0bc0e760e3be	2026-08-01 18:33:07.257369+00	00000000-0000-0000-0000-000000000001
b983caad-6117-4b8f-b1ab-d634eaa67358	00000000-0000-0000-0000-000000000102	BATCH-260725	\N	18000	18000	10.000	0.000	00000000-0000-0000-0000-000000000301	\N	38368f63-9951-41a4-9f62-1afa8b222360	2026-07-25 12:43:37.479087+00	00000000-0000-0000-0000-000000000001
50da73d1-02d3-4f91-bb4b-f1636741fd44	00000000-0000-0000-0000-000000000103	BATCH-260725	\N	28000	28000	100.000	98.000	00000000-0000-0000-0000-000000000301	\N	38368f63-9951-41a4-9f62-1afa8b222360	2026-07-25 12:43:37.479087+00	00000000-0000-0000-0000-000000000001
5e06d417-0f55-49ed-b46b-5b1ddf82709f	00000000-0000-0000-0000-000000000101	BATCH-260725	\N	22000	22000	10.000	3.000	00000000-0000-0000-0000-000000000301	\N	38368f63-9951-41a4-9f62-1afa8b222360	2026-07-25 12:43:37.479087+00	00000000-0000-0000-0000-000000000001
2b402913-cfee-4a0e-8a0a-717f4e2c729a	00000000-0000-0000-0000-000000000102	BATCH-260726	\N	18000	18000	100.000	98.000	00000000-0000-0000-0000-000000000301	\N	aa51afad-b03d-462f-8aae-a5a849fd9429	2026-07-26 10:15:17.51328+00	00000000-0000-0000-0000-000000000001
d53a3613-a26f-4a80-8394-756af69dc912	c3891d5e-a5ea-472a-9ba1-80b59266207a	PO-10345-c3891d5ea5ea472a9ba180b59266207a	\N	15000	25000	100.000	97.000	4e8ea130-d6dc-4ef5-b5df-3ffcff0001c0	\N	1ad0a64a-70be-475a-9795-d8b86129f06c	2026-08-01 18:34:19.574115+00	00000000-0000-0000-0000-000000000001
03718919-0f8b-45e2-8924-45cddb21162a	00000000-0000-0000-0000-000000000101	BATCH-260729	\N	22000	22000	1.000	1.000	00000000-0000-0000-0000-000000000301	\N	83c4bec6-e557-4ebb-b319-359c9505abb2	2026-07-29 06:14:17.975893+00	00000000-0000-0000-0000-000000000001
5bfaad3b-81fd-4999-8fec-f9a6de0b756a	00000000-0000-0000-0000-000000000104	BATCH-260729	\N	1800	1800	100.000	100.000	00000000-0000-0000-0000-000000000301	\N	8b344ca0-6121-4cdc-b7a4-4c392b92e29f	2026-07-29 06:19:42.204805+00	00000000-0000-0000-0000-000000000001
96564b2a-a66f-4575-9d6d-7c1e7045f5b3	00000000-0000-0000-0000-000000000103	BATCH-260729	\N	28000	28000	100.000	100.000	00000000-0000-0000-0000-000000000301	\N	1d841c12-802b-4250-82c8-28e807885f28	2026-07-29 06:28:39.942261+00	00000000-0000-0000-0000-000000000001
da6b4eef-a842-4468-8794-63aa7169fa02	00000000-0000-0000-0000-000000000102	BATCH-260729	\N	18000	18000	1.000	1.000	00000000-0000-0000-0000-000000000301	\N	1d841c12-802b-4250-82c8-28e807885f28	2026-07-29 06:28:39.942261+00	00000000-0000-0000-0000-000000000001
fa820481-dbf9-4c24-a116-a3cd3071b45b	00000000-0000-0000-0000-000000000101	BATCH-260729	\N	22000	22000	1.000	1.000	00000000-0000-0000-0000-000000000301	\N	1d841c12-802b-4250-82c8-28e807885f28	2026-07-29 06:28:39.942261+00	00000000-0000-0000-0000-000000000001
1feab575-6520-47a5-895c-02d6d7ee5200	00000000-0000-0000-0000-000000000104	BATCH-260725	\N	1800	1800	100.000	93.250	00000000-0000-0000-0000-000000000301	\N	36295608-81aa-49bd-85fe-d9debebab6c1	2026-07-25 12:44:42.213621+00	00000000-0000-0000-0000-000000000001
\.


--
-- Data for Name: production_consumption_items; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.production_consumption_items (id, production_log_id, product_id, quantity_consumed, tenant_id) FROM stdin;
\.


--
-- Data for Name: production_logs; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.production_logs (id, batch_reference, operator_id, additional_overhead_paisa, created_at, tenant_id) FROM stdin;
\.


--
-- Data for Name: production_yield_items; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.production_yield_items (id, production_log_id, product_id, quantity_produced, calculated_cost_price_paisa, target_batch_id, tenant_id) FROM stdin;
\.


--
-- Data for Name: products; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.products (id, category_id, name, sku, barcode, brand, base_unit, conversion_multiplier, show_on_webshop, created_at, updated_at, tenant_id, is_deleted, is_loose, short_code, min_stock_qty) FROM stdin;
00000000-0000-0000-0000-000000000101	00000000-0000-0000-0000-000000000401	Buffalo Milk	MILK-BUF	8900000000001	Copenhagen Mart	L	1	f	2026-07-25 11:13:08.767357+00	2026-07-25 11:13:08.767357+00	00000000-0000-0000-0000-000000000001	f	f	1001	10.000
00000000-0000-0000-0000-000000000102	00000000-0000-0000-0000-000000000401	Cow Milk	MILK-COW	8900000000002	Copenhagen Mart	L	1	f	2026-07-25 11:13:08.767357+00	2026-07-25 11:13:08.767357+00	00000000-0000-0000-0000-000000000001	f	f	1002	10.000
00000000-0000-0000-0000-000000000103	00000000-0000-0000-0000-000000000402	Banana	LOOSE-BAN	LOOSE-2001	Copenhagen Mart	kg	1	f	2026-07-25 11:13:08.767357+00	2026-07-25 11:13:08.767357+00	00000000-0000-0000-0000-000000000001	f	t	2001	10.000
00000000-0000-0000-0000-000000000104	00000000-0000-0000-0000-000000000403	Samosa	LOOSE-SAM	LOOSE-2002	Copenhagen Mart	pcs	1	f	2026-07-25 11:13:08.767357+00	2026-07-25 11:13:08.767357+00	00000000-0000-0000-0000-000000000001	f	t	2002	10.000
afd93f49-d720-4a71-bfb7-629c3b0fddf6	00000000-0000-0000-0000-000000000402	Lays Chips 100g	SKU-9f7ba1b2aee4	NB-d68b3959591646cc8	Lays	PCS	1	f	2026-08-01 16:52:44.582576+00	2026-08-01 16:52:44.582576+00	00000000-0000-0000-0000-000000000001	f	f	1235	10.000
c3891d5e-a5ea-472a-9ba1-80b59266207a	00000000-0000-0000-0000-000000000402	Cornitio	SKU-08ad60848102	NB-2ea8d1b8f5844e2e8	walls Ice Cream	PCS	1	f	2026-08-01 16:49:35.512909+00	2026-08-01 16:49:35.512909+00	00000000-0000-0000-0000-000000000001	f	f	123	10.000
\.


--
-- Data for Name: purchase_items; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.purchase_items (id, purchase_order_id, product_id, quantity_received, bonus_quantity, cost_price_per_unit_paisa, retail_price_per_unit_paisa, batch_id, tenant_id, batch_number, expiry_date, rack_location) FROM stdin;
91ade9d3-5a39-448f-9f9f-94fca681a49a	38368f63-9951-41a4-9f62-1afa8b222360	00000000-0000-0000-0000-000000000103	100.000	0.000	28000	28000	50da73d1-02d3-4f91-bb4b-f1636741fd44	00000000-0000-0000-0000-000000000001	BATCH-260725	\N	\N
b1d66f07-e4b9-4db0-a900-cc4ffde7e803	38368f63-9951-41a4-9f62-1afa8b222360	00000000-0000-0000-0000-000000000102	10.000	0.000	18000	18000	b983caad-6117-4b8f-b1ab-d634eaa67358	00000000-0000-0000-0000-000000000001	BATCH-260725	\N	\N
e759c84f-cb80-4386-afab-7924f18adc67	38368f63-9951-41a4-9f62-1afa8b222360	00000000-0000-0000-0000-000000000101	10.000	0.000	22000	22000	5e06d417-0f55-49ed-b46b-5b1ddf82709f	00000000-0000-0000-0000-000000000001	BATCH-260725	\N	\N
d3787e9c-ecb7-4918-9577-64e01455bfd7	36295608-81aa-49bd-85fe-d9debebab6c1	00000000-0000-0000-0000-000000000104	100.000	0.000	1800	1800	1feab575-6520-47a5-895c-02d6d7ee5200	00000000-0000-0000-0000-000000000001	BATCH-260725	\N	\N
059c3351-f65d-498f-a44e-09a7ba54017f	aa51afad-b03d-462f-8aae-a5a849fd9429	00000000-0000-0000-0000-000000000103	20.000	0.000	28000	28000	04ca6dea-04c8-4d79-a369-2f174456e6f4	00000000-0000-0000-0000-000000000001	BATCH-260726	\N	\N
6587a5b3-e45a-4556-8b09-79052bd123cb	aa51afad-b03d-462f-8aae-a5a849fd9429	00000000-0000-0000-0000-000000000102	100.000	0.000	18000	18000	2b402913-cfee-4a0e-8a0a-717f4e2c729a	00000000-0000-0000-0000-000000000001	BATCH-260726	\N	\N
bc2b74cb-77c1-4772-8a14-bdadc1ee9850	83c4bec6-e557-4ebb-b319-359c9505abb2	00000000-0000-0000-0000-000000000101	1.000	0.000	22000	22000	03718919-0f8b-45e2-8924-45cddb21162a	00000000-0000-0000-0000-000000000001	BATCH-260729	\N	\N
251fcb8b-c030-4d87-9a48-778a2cd27511	8b344ca0-6121-4cdc-b7a4-4c392b92e29f	00000000-0000-0000-0000-000000000104	100.000	0.000	1800	1800	5bfaad3b-81fd-4999-8fec-f9a6de0b756a	00000000-0000-0000-0000-000000000001	BATCH-260729	\N	\N
94f48bcc-6ba3-411d-baff-0d566803864e	1d841c12-802b-4250-82c8-28e807885f28	00000000-0000-0000-0000-000000000101	1.000	0.000	22000	22000	fa820481-dbf9-4c24-a116-a3cd3071b45b	00000000-0000-0000-0000-000000000001	BATCH-260729	\N	\N
b07e146b-b3c8-47d3-ae1a-9c416f82adc1	1d841c12-802b-4250-82c8-28e807885f28	00000000-0000-0000-0000-000000000103	100.000	0.000	28000	28000	96564b2a-a66f-4575-9d6d-7c1e7045f5b3	00000000-0000-0000-0000-000000000001	BATCH-260729	\N	\N
daef95e0-090e-4da5-b617-d85084c98717	1d841c12-802b-4250-82c8-28e807885f28	00000000-0000-0000-0000-000000000102	1.000	0.000	18000	18000	da6b4eef-a842-4468-8794-63aa7169fa02	00000000-0000-0000-0000-000000000001	BATCH-260729	\N	\N
d5d0fed8-9966-4f74-8e4b-1f5c6589c138	91785450-5722-421f-8acb-ea6157a31c6d	afd93f49-d720-4a71-bfb7-629c3b0fddf6	1.000	0.000	7000	10000	1e44615d-837b-40be-bc71-9ba7d17ab80e	00000000-0000-0000-0000-000000000001		\N	\N
1f4c13a3-7a2e-4823-afdb-62b8efb887b9	8b95d73b-f532-48ee-9864-66f144713ef0	00000000-0000-0000-0000-000000000101	21.000	0.000	22000	22000	42bb22b9-7197-4b56-954d-1f2edfece4c5	00000000-0000-0000-0000-000000000001	BATCH-260801	\N	\N
240cad29-f05e-44ad-819b-491e02da17df	34819be3-8196-4ded-8b66-fe9c43422e9d	afd93f49-d720-4a71-bfb7-629c3b0fddf6	100.000	0.000	10000	10000	2c12cb47-dff3-41ae-a5de-9ed724fd0655	00000000-0000-0000-0000-000000000001	BATCH-260801	\N	\N
8f342cfc-2961-4e44-93db-546ee49c4e9c	de18dbde-621f-404e-b446-7782d8865d72	afd93f49-d720-4a71-bfb7-629c3b0fddf6	100.000	0.000	7000	12000	28939ee2-d067-4516-8f68-e1401dc2d9de	00000000-0000-0000-0000-000000000001		\N	\N
ffef72e4-3ed8-4af1-b9f9-7b1fcc66c950	f693a63a-5dc5-4d30-b964-0bc0e760e3be	c3891d5e-a5ea-472a-9ba1-80b59266207a	1.000	0.000	10000	15000	34abfea8-c687-4586-8da1-a112a57a5712	00000000-0000-0000-0000-000000000001	BATCH-260801	\N	\N
e79d44db-5aeb-4837-8674-3fd15a558d86	1ad0a64a-70be-475a-9795-d8b86129f06c	c3891d5e-a5ea-472a-9ba1-80b59266207a	100.000	0.000	15000	25000	d53a3613-a26f-4a80-8394-756af69dc912	00000000-0000-0000-0000-000000000001		\N	\N
\.


--
-- Data for Name: purchase_orders; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.purchase_orders (id, supplier_invoice_no, supplier_id, receiver_id, sub_total_paisa, discount_paisa, net_payable_paisa, payment_status, created_at, tenant_id, is_received, amount_paid_paisa) FROM stdin;
38368f63-9951-41a4-9f62-1afa8b222360	INV-20260725-1742	00000000-0000-0000-0000-000000000301	67222182-84f3-416a-953f-e21a02577d13	3200000	0	3200000	CREDIT	2026-07-24 12:00:00+00	00000000-0000-0000-0000-000000000001	t	0
36295608-81aa-49bd-85fe-d9debebab6c1	INV-20260725-1743	00000000-0000-0000-0000-000000000301	67222182-84f3-416a-953f-e21a02577d13	180000	0	180000	CREDIT	2026-07-24 12:00:00+00	00000000-0000-0000-0000-000000000001	t	0
aa51afad-b03d-462f-8aae-a5a849fd9429	INV-20260726-1514	00000000-0000-0000-0000-000000000301	67222182-84f3-416a-953f-e21a02577d13	2360000	0	2360000	CREDIT	2026-07-25 12:00:00+00	00000000-0000-0000-0000-000000000001	t	0
83c4bec6-e557-4ebb-b319-359c9505abb2	INV-20260729-1113	00000000-0000-0000-0000-000000000301	67222182-84f3-416a-953f-e21a02577d13	22000	0	22000	CREDIT	2026-07-29 06:14:16.661708+00	00000000-0000-0000-0000-000000000001	t	0
8b344ca0-6121-4cdc-b7a4-4c392b92e29f	INV-20260729-1114	00000000-0000-0000-0000-000000000301	67222182-84f3-416a-953f-e21a02577d13	180000	0	180000	CREDIT	2026-07-29 06:19:42.064299+00	00000000-0000-0000-0000-000000000001	t	0
1d841c12-802b-4250-82c8-28e807885f28	INV-20260729-1120	00000000-0000-0000-0000-000000000301	67222182-84f3-416a-953f-e21a02577d13	2840000	0	2840000	CREDIT	2026-07-29 06:28:39.680027+00	00000000-0000-0000-0000-000000000001	t	0
91785450-5722-421f-8acb-ea6157a31c6d	090-99	9a3db5ed-d7d5-4241-8db9-802d70bddb84	7025ab9a-f599-4b3b-8e56-9e008e81feec	7000	0	7000	CREDIT	2026-08-01 00:00:00+00	00000000-0000-0000-0000-000000000001	t	0
8b95d73b-f532-48ee-9864-66f144713ef0	INV-20260801-2155	00000000-0000-0000-0000-000000000301	67222182-84f3-416a-953f-e21a02577d13	462000	0	462000	CREDIT	2026-08-01 17:00:08.695519+00	00000000-0000-0000-0000-000000000001	t	0
34819be3-8196-4ded-8b66-fe9c43422e9d	INV-20260801-2249	9a3db5ed-d7d5-4241-8db9-802d70bddb84	67222182-84f3-416a-953f-e21a02577d13	1000000	0	1000000	CREDIT	2026-08-01 17:49:42.696076+00	00000000-0000-0000-0000-000000000001	t	0
de18dbde-621f-404e-b446-7782d8865d72	098	9a3db5ed-d7d5-4241-8db9-802d70bddb84	7025ab9a-f599-4b3b-8e56-9e008e81feec	700000	0	700000	CREDIT	2026-08-01 00:00:00+00	00000000-0000-0000-0000-000000000001	t	0
f693a63a-5dc5-4d30-b964-0bc0e760e3be	INV-20260801-2332	4e8ea130-d6dc-4ef5-b5df-3ffcff0001c0	67222182-84f3-416a-953f-e21a02577d13	10000	0	10000	CREDIT	2026-08-01 18:33:06.885255+00	00000000-0000-0000-0000-000000000001	t	0
1ad0a64a-70be-475a-9795-d8b86129f06c	10345	4e8ea130-d6dc-4ef5-b5df-3ffcff0001c0	7025ab9a-f599-4b3b-8e56-9e008e81feec	1500000	0	1500000	CREDIT	2026-08-01 00:00:00+00	00000000-0000-0000-0000-000000000001	t	0
\.


--
-- Data for Name: purchase_return_items; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.purchase_return_items (id, purchase_return_id, product_id, batch_id, quantity, cost_per_unit_paisa, tenant_id) FROM stdin;
\.


--
-- Data for Name: purchase_returns; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.purchase_returns (id, original_purchase_order_id, manager_id, supplier_id, total_credit_deduction_paisa, created_at, tenant_id) FROM stdin;
\.


--
-- Data for Name: roles; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.roles (id, role_name, created_at, tenant_id) FROM stdin;
1fc4e707-cb9d-4bea-a209-00279add3b0d	Manager	2026-07-25 11:13:08.767357+00	00000000-0000-0000-0000-000000000001
c42f74b9-b665-477e-8c5a-2eddeee6e085	Cashier	2026-07-25 11:13:08.767357+00	00000000-0000-0000-0000-000000000001
f3f36891-33a8-4376-b7da-ebe59fb1edee	Owner	2026-07-25 11:13:08.767357+00	00000000-0000-0000-0000-000000000001
\.


--
-- Data for Name: sales_invoices; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.sales_invoices (invoice_no, shift_id, terminal_id, cashier_id, customer_id, total_amount_paisa, tax_amount_paisa, discount_amount_paisa, discount_reason, receipt_number, payment_method, created_at, tenant_id, amount_paid_paisa) FROM stdin;
POS-20260728093434699	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	00000000-0000-0000-0000-000000000501	19800	0	0	\N	POS-20260728093434699	CREDIT	2026-07-28 09:34:34.74364+00	00000000-0000-0000-0000-000000000001	0
POS-20260728093503740	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	00000000-0000-0000-0000-000000000501	41800	0	0	\N	POS-20260728093503740	CREDIT	2026-07-28 09:35:03.755363+00	00000000-0000-0000-0000-000000000001	0
POS-20260728162629268	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	00000000-0000-0000-0000-000000000501	41800	0	0	\N	POS-20260728162629268	CREDIT	2026-07-28 16:26:29.368615+00	00000000-0000-0000-0000-000000000001	0
POS-20260729050728574	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	00000000-0000-0000-0000-000000000501	40000	0	0	\N	POS-20260729050728574	CREDIT	2026-07-29 05:07:28.604747+00	00000000-0000-0000-0000-000000000001	0
POS-20260729052558867	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	00000000-0000-0000-0000-000000000501	1800	0	0	\N	POS-20260729052558867	CREDIT	2026-07-29 05:25:58.911288+00	00000000-0000-0000-0000-000000000001	0
POS-20260729052638728	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	00000000-0000-0000-0000-000000000501	69800	0	0	\N	POS-20260729052638728	CREDIT	2026-07-29 05:26:38.752414+00	00000000-0000-0000-0000-000000000001	0
POS-20260729052807378	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	00000000-0000-0000-0000-000000000501	59800	0	0	\N	POS-20260729052807378	CREDIT	2026-07-29 05:28:07.399154+00	00000000-0000-0000-0000-000000000001	0
POS-20260729053301076	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	00000000-0000-0000-0000-000000000501	41800	0	0	\N	POS-20260729053301076	CREDIT	2026-07-29 05:33:01.187446+00	00000000-0000-0000-0000-000000000001	0
POS-20260729053452286	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	00000000-0000-0000-0000-000000000501	40000	0	0	\N	POS-20260729053452286	CREDIT	2026-07-29 05:34:52.298908+00	00000000-0000-0000-0000-000000000001	0
POS-20260730131232419	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	00000000-0000-0000-0000-000000000501	1800	0	0	\N	POS-20260730131232419	CREDIT	2026-07-30 13:12:32.941131+00	00000000-0000-0000-0000-000000000001	0
POS-20260725114229848	397fc0c3-9af8-4e1a-800b-52928df0b0cf	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	\N	117600	0	0	\N	POS-20260725114229848	CASH	2026-07-25 11:42:29.932369+00	00000000-0000-0000-0000-000000000001	117600
POS-20260726101235813	397fc0c3-9af8-4e1a-800b-52928df0b0cf	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	\N	68000	0	0	\N	POS-20260726101235813	CASH	2026-07-26 10:12:35.882285+00	00000000-0000-0000-0000-000000000001	68000
POS-20260726101308608	397fc0c3-9af8-4e1a-800b-52928df0b0cf	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	00000000-0000-0000-0000-000000000501	19800	0	0	\N	POS-20260726101308608	CASH	2026-07-26 10:13:08.638686+00	00000000-0000-0000-0000-000000000001	19800
POS-20260728093244680	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	00000000-0000-0000-0000-000000000501	69800	0	0	\N	POS-20260728093244680	CASH	2026-07-28 09:32:45.048319+00	00000000-0000-0000-0000-000000000001	69800
POS-20260729050714585	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	\N	19800	0	0	\N	POS-20260729050714585	BANK_TRANSFER	2026-07-29 05:07:14.661963+00	00000000-0000-0000-0000-000000000001	19800
POS-20260729050811071	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	00000000-0000-0000-0000-000000000501	40000	0	0	\N	POS-20260729050811071	JAZZCASH	2026-07-29 05:08:11.08702+00	00000000-0000-0000-0000-000000000001	40000
POS-20260729050841688	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	\N	19800	0	0	\N	POS-20260729050841688	CASH	2026-07-29 05:08:41.707385+00	00000000-0000-0000-0000-000000000001	19800
POS-20260729161859674	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	\N	40000	0	0	\N	POS-20260729161859674	CASH	2026-07-29 16:18:59.931342+00	00000000-0000-0000-0000-000000000001	40000
POS-20260801174910855	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	\N	10000	0	0	\N	POS-20260801174910855	CASH	2026-08-01 17:49:11.217621+00	00000000-0000-0000-0000-000000000001	10000
POS-20260801175024761	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	\N	130000	0	0	\N	POS-20260801175024761	CASH	2026-08-01 17:50:24.832876+00	00000000-0000-0000-0000-000000000001	130000
POS-20260801183518927	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	\N	15000	0	0	\N	POS-20260801183518927	CASH	2026-08-01 18:35:19.077558+00	00000000-0000-0000-0000-000000000001	15000
POS-20260801202203648	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	2cd5a00a-3c63-4b20-bbab-8e225c89a1ed	65000	0	0	\N	POS-20260801202203648	CREDIT	2026-08-01 20:22:04.21127+00	00000000-0000-0000-0000-000000000001	0
POS-20260801202230688	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	2cd5a00a-3c63-4b20-bbab-8e225c89a1ed	93000	0	0	\N	POS-20260801202230688	CREDIT	2026-08-01 20:22:30.776776+00	00000000-0000-0000-0000-000000000001	0
POS-20260801202416953	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	2cd5a00a-3c63-4b20-bbab-8e225c89a1ed	43000	0	0	\N	POS-20260801202416953	JAZZCASH	2026-08-01 20:24:17.014832+00	00000000-0000-0000-0000-000000000001	43000
POS-20260801203114999	e39cecc0-291c-4b40-b46f-ef4fa1e043ad	00000000-0000-0000-0000-000000000010	67222182-84f3-416a-953f-e21a02577d13	2cd5a00a-3c63-4b20-bbab-8e225c89a1ed	43000	0	0	\N	POS-20260801203114999	CREDIT	2026-08-01 20:31:15.104027+00	00000000-0000-0000-0000-000000000001	0
\.


--
-- Data for Name: sales_items; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.sales_items (id, invoice_no, product_id, batch_id, quantity, unit_price_paisa, discount_applied_paisa, tenant_id, unit_cost_paisa) FROM stdin;
0b0ce80b-ff21-43aa-b942-658d7584340d	POS-20260725114229848	00000000-0000-0000-0000-000000000104	00000000-0000-0000-0000-000000000204	2.000	1800	0	00000000-0000-0000-0000-000000000001	1200
7fc4968e-94b4-4869-915b-c1e9e85dd77d	POS-20260725114229848	00000000-0000-0000-0000-000000000101	00000000-0000-0000-0000-000000000201	1.000	22000	0	00000000-0000-0000-0000-000000000001	18000
9b502558-8d38-465d-b55f-2f793c32dd50	POS-20260725114229848	00000000-0000-0000-0000-000000000102	00000000-0000-0000-0000-000000000202	2.000	18000	0	00000000-0000-0000-0000-000000000001	14000
f9cba9ae-de55-489b-84e0-fe7cb8531e49	POS-20260725114229848	00000000-0000-0000-0000-000000000103	00000000-0000-0000-0000-000000000203	2.000	28000	0	00000000-0000-0000-0000-000000000001	22000
0a41c03c-9781-47c5-9c48-c76dbc28d8fb	POS-20260726101235813	00000000-0000-0000-0000-000000000101	00000000-0000-0000-0000-000000000201	1.000	22000	0	00000000-0000-0000-0000-000000000001	18000
24375a38-d00d-427f-876c-fb91dbddb6d4	POS-20260726101235813	00000000-0000-0000-0000-000000000102	00000000-0000-0000-0000-000000000202	1.000	18000	0	00000000-0000-0000-0000-000000000001	14000
ae7010a1-0fb8-4732-82c5-c3d854ea6cb3	POS-20260726101235813	00000000-0000-0000-0000-000000000103	00000000-0000-0000-0000-000000000203	1.000	28000	0	00000000-0000-0000-0000-000000000001	22000
8d271b5e-b26e-4e33-bb70-af931d871cf8	POS-20260726101308608	00000000-0000-0000-0000-000000000102	00000000-0000-0000-0000-000000000202	1.000	18000	0	00000000-0000-0000-0000-000000000001	14000
c2ef3f74-0c2e-415b-9977-b5c137e4ee61	POS-20260726101308608	00000000-0000-0000-0000-000000000104	00000000-0000-0000-0000-000000000204	1.000	1800	0	00000000-0000-0000-0000-000000000001	1200
5772999c-1f7f-4ef5-8210-bee943004a13	POS-20260728093244680	00000000-0000-0000-0000-000000000103	50da73d1-02d3-4f91-bb4b-f1636741fd44	1.000	28000	0	00000000-0000-0000-0000-000000000001	28000
6e3cea6d-942f-4ab7-a555-2376f4e60f84	POS-20260728093244680	00000000-0000-0000-0000-000000000102	b983caad-6117-4b8f-b1ab-d634eaa67358	1.000	18000	0	00000000-0000-0000-0000-000000000001	18000
813dc0b9-1bb2-4743-9c34-cee494725581	POS-20260728093244680	00000000-0000-0000-0000-000000000101	5e06d417-0f55-49ed-b46b-5b1ddf82709f	1.000	22000	0	00000000-0000-0000-0000-000000000001	22000
e710de05-2aa7-41bb-9150-810d8a323e65	POS-20260728093244680	00000000-0000-0000-0000-000000000104	1feab575-6520-47a5-895c-02d6d7ee5200	1.000	1800	0	00000000-0000-0000-0000-000000000001	1800
3aa6d700-fa8f-4fa9-a723-15ddd619a80a	POS-20260728093434699	00000000-0000-0000-0000-000000000104	1feab575-6520-47a5-895c-02d6d7ee5200	1.000	1800	0	00000000-0000-0000-0000-000000000001	1800
69316dbe-7e34-435d-8b3d-125f9d396ff1	POS-20260728093434699	00000000-0000-0000-0000-000000000102	b983caad-6117-4b8f-b1ab-d634eaa67358	1.000	18000	0	00000000-0000-0000-0000-000000000001	18000
11e07c37-810a-4e1d-bd90-e869e24ab08e	POS-20260728093503740	00000000-0000-0000-0000-000000000101	5e06d417-0f55-49ed-b46b-5b1ddf82709f	1.000	22000	0	00000000-0000-0000-0000-000000000001	22000
551f2e36-359d-4855-9f6b-5ef2e55e251c	POS-20260728093503740	00000000-0000-0000-0000-000000000102	b983caad-6117-4b8f-b1ab-d634eaa67358	1.000	18000	0	00000000-0000-0000-0000-000000000001	18000
77870352-08b0-42f0-8313-cd9e6352bc54	POS-20260728093503740	00000000-0000-0000-0000-000000000104	1feab575-6520-47a5-895c-02d6d7ee5200	1.000	1800	0	00000000-0000-0000-0000-000000000001	1800
21cc21e1-69d5-4044-b5d4-8c6af072cdee	POS-20260728162629268	00000000-0000-0000-0000-000000000102	b983caad-6117-4b8f-b1ab-d634eaa67358	1.000	18000	0	00000000-0000-0000-0000-000000000001	18000
72bba90f-4325-4328-87da-a46d2e6bd45f	POS-20260728162629268	00000000-0000-0000-0000-000000000101	5e06d417-0f55-49ed-b46b-5b1ddf82709f	1.000	22000	0	00000000-0000-0000-0000-000000000001	22000
9b0954ee-e44d-4bfb-914a-71c862f0c39f	POS-20260728162629268	00000000-0000-0000-0000-000000000104	1feab575-6520-47a5-895c-02d6d7ee5200	1.000	1800	0	00000000-0000-0000-0000-000000000001	1800
021a6131-688e-4eef-b6c3-b53b96805b0d	POS-20260729050714585	00000000-0000-0000-0000-000000000104	1feab575-6520-47a5-895c-02d6d7ee5200	1.000	1800	0	00000000-0000-0000-0000-000000000001	1800
d7d3cbb1-9862-4046-9b92-c1c968ad0da7	POS-20260729050714585	00000000-0000-0000-0000-000000000102	b983caad-6117-4b8f-b1ab-d634eaa67358	1.000	18000	0	00000000-0000-0000-0000-000000000001	18000
9d82ccf0-9c62-4cc6-b5e7-f75e904eea75	POS-20260729050728574	00000000-0000-0000-0000-000000000101	5e06d417-0f55-49ed-b46b-5b1ddf82709f	1.000	22000	0	00000000-0000-0000-0000-000000000001	22000
e83f4103-d3ca-448c-beb1-232d5b9ebe2d	POS-20260729050728574	00000000-0000-0000-0000-000000000102	b983caad-6117-4b8f-b1ab-d634eaa67358	1.000	18000	0	00000000-0000-0000-0000-000000000001	18000
4ab56557-8cc0-4bb9-92b8-a432c75fec02	POS-20260729050811071	00000000-0000-0000-0000-000000000101	5e06d417-0f55-49ed-b46b-5b1ddf82709f	1.000	22000	0	00000000-0000-0000-0000-000000000001	22000
87f7325e-ee47-41c9-b3b5-3c1a9c0c3e75	POS-20260729050811071	00000000-0000-0000-0000-000000000102	b983caad-6117-4b8f-b1ab-d634eaa67358	1.000	18000	0	00000000-0000-0000-0000-000000000001	18000
8ef9b938-2d2c-4657-8162-b3ba73d206bb	POS-20260729050841688	00000000-0000-0000-0000-000000000102	b983caad-6117-4b8f-b1ab-d634eaa67358	1.000	18000	0	00000000-0000-0000-0000-000000000001	18000
c8dd9819-fcea-401a-991f-73fb50e01ac9	POS-20260729050841688	00000000-0000-0000-0000-000000000104	1feab575-6520-47a5-895c-02d6d7ee5200	1.000	1800	0	00000000-0000-0000-0000-000000000001	1800
8b55ac54-0dd4-4443-b85c-6dc413780ed1	POS-20260729052558867	00000000-0000-0000-0000-000000000104	1feab575-6520-47a5-895c-02d6d7ee5200	1.000	1800	0	00000000-0000-0000-0000-000000000001	1800
2163652f-c504-487d-a362-2b362b74adc0	POS-20260729052638728	00000000-0000-0000-0000-000000000101	5e06d417-0f55-49ed-b46b-5b1ddf82709f	1.000	22000	0	00000000-0000-0000-0000-000000000001	22000
6ff74747-916c-4b38-b504-eb442e31ea6e	POS-20260729052638728	00000000-0000-0000-0000-000000000103	50da73d1-02d3-4f91-bb4b-f1636741fd44	1.000	28000	0	00000000-0000-0000-0000-000000000001	28000
b147525d-9d30-43f6-8d59-47de42ddb6ae	POS-20260729052638728	00000000-0000-0000-0000-000000000102	b983caad-6117-4b8f-b1ab-d634eaa67358	1.000	18000	0	00000000-0000-0000-0000-000000000001	18000
c879cbb5-0ee0-45b8-8b1a-bf63baf39344	POS-20260729052638728	00000000-0000-0000-0000-000000000104	1feab575-6520-47a5-895c-02d6d7ee5200	1.000	1800	0	00000000-0000-0000-0000-000000000001	1800
5be40440-70d5-4cf7-ba40-2df0e6085d3b	POS-20260729052807378	00000000-0000-0000-0000-000000000104	1feab575-6520-47a5-895c-02d6d7ee5200	1.000	1800	0	00000000-0000-0000-0000-000000000001	1800
c013d053-4cad-448d-94a8-26befb05f6d7	POS-20260729052807378	00000000-0000-0000-0000-000000000102	b983caad-6117-4b8f-b1ab-d634eaa67358	2.000	18000	0	00000000-0000-0000-0000-000000000001	18000
da26b4d6-858b-4292-b9aa-25acbd115bdb	POS-20260729052807378	00000000-0000-0000-0000-000000000101	5e06d417-0f55-49ed-b46b-5b1ddf82709f	1.000	22000	0	00000000-0000-0000-0000-000000000001	22000
6509e306-dcde-42b3-b1cf-6c7d2cd50788	POS-20260729053301076	00000000-0000-0000-0000-000000000101	5e06d417-0f55-49ed-b46b-5b1ddf82709f	1.000	22000	0	00000000-0000-0000-0000-000000000001	22000
b639e014-8101-409a-a6b0-35ba14fe13a9	POS-20260729053301076	00000000-0000-0000-0000-000000000104	1feab575-6520-47a5-895c-02d6d7ee5200	1.000	1800	0	00000000-0000-0000-0000-000000000001	1800
f42cf854-8488-4ec7-8f3f-ba9cb3a449b2	POS-20260729053301076	00000000-0000-0000-0000-000000000102	b983caad-6117-4b8f-b1ab-d634eaa67358	1.000	18000	0	00000000-0000-0000-0000-000000000001	18000
32dc1cf7-9061-4ad1-adba-580ca717ce97	POS-20260729053452286	00000000-0000-0000-0000-000000000102	b983caad-6117-4b8f-b1ab-d634eaa67358	1.000	18000	0	00000000-0000-0000-0000-000000000001	18000
ad1f3782-a805-440d-9734-b55647b1dc5a	POS-20260729053452286	00000000-0000-0000-0000-000000000101	5e06d417-0f55-49ed-b46b-5b1ddf82709f	1.000	22000	0	00000000-0000-0000-0000-000000000001	22000
71f4b09b-3b44-4247-b729-86424b623bc0	POS-20260729161859674	00000000-0000-0000-0000-000000000101	5e06d417-0f55-49ed-b46b-5b1ddf82709f	1.000	22000	0	00000000-0000-0000-0000-000000000001	22000
be7dea4b-1adb-4f97-a07b-6615cab5c092	POS-20260729161859674	00000000-0000-0000-0000-000000000102	b983caad-6117-4b8f-b1ab-d634eaa67358	1.000	18000	0	00000000-0000-0000-0000-000000000001	18000
73db4dfd-2716-43a2-bb95-e8a21b044e56	POS-20260730131232419	00000000-0000-0000-0000-000000000104	1feab575-6520-47a5-895c-02d6d7ee5200	1.000	1800	0	00000000-0000-0000-0000-000000000001	1800
88a69e21-2a47-4c86-bd78-3bffea576ac8	POS-20260801174910855	afd93f49-d720-4a71-bfb7-629c3b0fddf6	1e44615d-837b-40be-bc71-9ba7d17ab80e	1.000	10000	0	00000000-0000-0000-0000-000000000001	7000
c7bd8db9-4321-4ff0-9670-7796a7be80fe	POS-20260801175024761	afd93f49-d720-4a71-bfb7-629c3b0fddf6	2c12cb47-dff3-41ae-a5de-9ed724fd0655	13.000	10000	0	00000000-0000-0000-0000-000000000001	10000
601ed7a1-08a3-4f1a-b91b-f4367b5e639a	POS-20260801183518927	c3891d5e-a5ea-472a-9ba1-80b59266207a	34abfea8-c687-4586-8da1-a112a57a5712	1.000	15000	0	00000000-0000-0000-0000-000000000001	10000
3b010eb0-2c7e-4671-9add-24f6b4d31492	POS-20260801202203648	00000000-0000-0000-0000-000000000101	5e06d417-0f55-49ed-b46b-5b1ddf82709f	1.000	22000	0	00000000-0000-0000-0000-000000000001	22000
4b38c24f-e45f-4f51-90fa-29f62df786bb	POS-20260801202203648	c3891d5e-a5ea-472a-9ba1-80b59266207a	d53a3613-a26f-4a80-8394-756af69dc912	1.000	25000	0	00000000-0000-0000-0000-000000000001	15000
600b6545-990d-4308-b487-00afc71b3d22	POS-20260801202203648	00000000-0000-0000-0000-000000000102	b983caad-6117-4b8f-b1ab-d634eaa67358	1.000	18000	0	00000000-0000-0000-0000-000000000001	18000
34e53774-df0a-43b0-bb19-c49ae0ece549	POS-20260801202230688	00000000-0000-0000-0000-000000000101	5e06d417-0f55-49ed-b46b-5b1ddf82709f	1.000	22000	0	00000000-0000-0000-0000-000000000001	22000
414c637b-1670-4cda-b15c-1a66578eb1b6	POS-20260801202230688	00000000-0000-0000-0000-000000000103	50da73d1-02d3-4f91-bb4b-f1636741fd44	1.000	28000	0	00000000-0000-0000-0000-000000000001	28000
ddc75d9d-09a1-4488-bcb1-93289163e2fb	POS-20260801202230688	c3891d5e-a5ea-472a-9ba1-80b59266207a	d53a3613-a26f-4a80-8394-756af69dc912	1.000	25000	0	00000000-0000-0000-0000-000000000001	15000
efaabff0-f720-45c1-8d16-4ccce94e7e0a	POS-20260801202230688	00000000-0000-0000-0000-000000000102	2b402913-cfee-4a0e-8a0a-717f4e2c729a	1.000	18000	0	00000000-0000-0000-0000-000000000001	18000
5a22c2a8-d22d-4ced-8f08-d37620b8ac80	POS-20260801202416953	c3891d5e-a5ea-472a-9ba1-80b59266207a	d53a3613-a26f-4a80-8394-756af69dc912	1.000	25000	0	00000000-0000-0000-0000-000000000001	15000
628c6089-6df0-4c1b-9142-4f4b6600b095	POS-20260801202416953	00000000-0000-0000-0000-000000000102	2b402913-cfee-4a0e-8a0a-717f4e2c729a	1.000	18000	0	00000000-0000-0000-0000-000000000001	18000
a64bf4bf-7e2b-42c5-ba75-ad50c1980737	POS-20260801203114999	00000000-0000-0000-0000-000000000102	2b402913-cfee-4a0e-8a0a-717f4e2c729a	1.000	18000	0	00000000-0000-0000-0000-000000000001	18000
b7ebcb06-99d8-4267-ae65-48de7e1e21c7	POS-20260801203114999	c3891d5e-a5ea-472a-9ba1-80b59266207a	d53a3613-a26f-4a80-8394-756af69dc912	1.000	25000	0	00000000-0000-0000-0000-000000000001	15000
\.


--
-- Data for Name: sales_return_items; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.sales_return_items (id, sales_return_id, product_id, batch_id, quantity, refund_unit_price_paisa, return_condition, tenant_id) FROM stdin;
0f65e20e-dc93-4083-ab85-1e9a46c312fa	29d4bee4-f783-47c4-b6d1-67b58d6a0714	00000000-0000-0000-0000-000000000101	5e06d417-0f55-49ed-b46b-5b1ddf82709f	1.000	22000	GOOD	00000000-0000-0000-0000-000000000001
23127253-1c30-4180-8771-a4672aa45e21	29d4bee4-f783-47c4-b6d1-67b58d6a0714	00000000-0000-0000-0000-000000000102	b983caad-6117-4b8f-b1ab-d634eaa67358	1.000	18000	GOOD	00000000-0000-0000-0000-000000000001
48475a0d-8111-4b70-b647-ddc6653c25f4	29d4bee4-f783-47c4-b6d1-67b58d6a0714	00000000-0000-0000-0000-000000000104	1feab575-6520-47a5-895c-02d6d7ee5200	1.000	1800	GOOD	00000000-0000-0000-0000-000000000001
a13d030d-2bb2-4171-a06f-933069957d04	45f7be49-f997-46c5-8811-fb2048d0fbc7	00000000-0000-0000-0000-000000000102	b983caad-6117-4b8f-b1ab-d634eaa67358	1.000	18000	GOOD	00000000-0000-0000-0000-000000000001
e3586816-26c4-43b8-8732-26a196c4a374	45f7be49-f997-46c5-8811-fb2048d0fbc7	00000000-0000-0000-0000-000000000104	1feab575-6520-47a5-895c-02d6d7ee5200	0.250	1800	GOOD	00000000-0000-0000-0000-000000000001
5eca3c0d-2d4c-47bd-87c9-996f1f845655	742e43fc-7fd1-4096-92a6-68aaf10ff61a	00000000-0000-0000-0000-000000000101	5e06d417-0f55-49ed-b46b-5b1ddf82709f	1.000	22000	GOOD	00000000-0000-0000-0000-000000000001
62836a92-b2bb-4638-b2ae-190b0fc6f7b6	742e43fc-7fd1-4096-92a6-68aaf10ff61a	00000000-0000-0000-0000-000000000104	1feab575-6520-47a5-895c-02d6d7ee5200	1.000	1800	GOOD	00000000-0000-0000-0000-000000000001
926ca995-78ce-478c-bc82-cee622bbab42	742e43fc-7fd1-4096-92a6-68aaf10ff61a	00000000-0000-0000-0000-000000000102	b983caad-6117-4b8f-b1ab-d634eaa67358	1.000	18000	GOOD	00000000-0000-0000-0000-000000000001
a58d09e4-3a42-4fcd-bce4-e8e539ac9e4c	742e43fc-7fd1-4096-92a6-68aaf10ff61a	00000000-0000-0000-0000-000000000103	50da73d1-02d3-4f91-bb4b-f1636741fd44	1.000	28000	GOOD	00000000-0000-0000-0000-000000000001
0c8d5e8c-b7e5-4280-a112-630660142cb9	d66048f4-db92-4eca-9e0b-1626e19046f0	00000000-0000-0000-0000-000000000104	1feab575-6520-47a5-895c-02d6d7ee5200	1.000	1800	GOOD	00000000-0000-0000-0000-000000000001
9b607486-adf9-449a-9fce-d9cf5e301b8b	d66048f4-db92-4eca-9e0b-1626e19046f0	00000000-0000-0000-0000-000000000101	5e06d417-0f55-49ed-b46b-5b1ddf82709f	1.000	22000	GOOD	00000000-0000-0000-0000-000000000001
5283f8f0-b559-42b6-a3ef-a40a563b8dc2	5280a2c9-4ef5-4724-95fe-fb3630b67d10	00000000-0000-0000-0000-000000000102	b983caad-6117-4b8f-b1ab-d634eaa67358	1.000	18000	GOOD	00000000-0000-0000-0000-000000000001
a7f286ac-0144-436e-8974-6484cbc50eda	5280a2c9-4ef5-4724-95fe-fb3630b67d10	00000000-0000-0000-0000-000000000101	5e06d417-0f55-49ed-b46b-5b1ddf82709f	1.000	22000	GOOD	00000000-0000-0000-0000-000000000001
bb82d682-ce8b-4d34-b7a6-405409936b69	d436ef01-55b7-4828-96bf-56e2c79fe309	00000000-0000-0000-0000-000000000104	1feab575-6520-47a5-895c-02d6d7ee5200	1.000	1800	GOOD	00000000-0000-0000-0000-000000000001
0664b47d-ebfa-4b76-8310-5f69d6d407ed	f0dd1437-49fb-4e59-a372-1de8e3f5ab87	00000000-0000-0000-0000-000000000102	b983caad-6117-4b8f-b1ab-d634eaa67358	1.000	18000	GOOD	00000000-0000-0000-0000-000000000001
7bb89d7e-6aa6-4867-a93b-e432abc0b07b	f0dd1437-49fb-4e59-a372-1de8e3f5ab87	00000000-0000-0000-0000-000000000101	5e06d417-0f55-49ed-b46b-5b1ddf82709f	1.000	22000	GOOD	00000000-0000-0000-0000-000000000001
1325233c-2755-4b0a-b76b-e0100e1c1e0b	f7b44cf0-2fe1-4e4e-a408-e6b1afe6eca2	c3891d5e-a5ea-472a-9ba1-80b59266207a	d53a3613-a26f-4a80-8394-756af69dc912	1.000	25000	GOOD	00000000-0000-0000-0000-000000000001
69f02d7f-a229-477f-8062-5dd52ef4935e	f7b44cf0-2fe1-4e4e-a408-e6b1afe6eca2	00000000-0000-0000-0000-000000000102	2b402913-cfee-4a0e-8a0a-717f4e2c729a	1.000	18000	GOOD	00000000-0000-0000-0000-000000000001
\.


--
-- Data for Name: sales_returns; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.sales_returns (id, original_invoice_no, cashier_id, customer_id, total_refund_paisa, created_at, tenant_id) FROM stdin;
29d4bee4-f783-47c4-b6d1-67b58d6a0714	POS-20260728093503740	67222182-84f3-416a-953f-e21a02577d13	00000000-0000-0000-0000-000000000501	41800	2026-07-28 09:35:46.812354+00	00000000-0000-0000-0000-000000000001
45f7be49-f997-46c5-8811-fb2048d0fbc7	POS-20260728093434699	67222182-84f3-416a-953f-e21a02577d13	00000000-0000-0000-0000-000000000501	18450	2026-07-28 16:02:02.551743+00	00000000-0000-0000-0000-000000000001
742e43fc-7fd1-4096-92a6-68aaf10ff61a	POS-20260729052638728	67222182-84f3-416a-953f-e21a02577d13	00000000-0000-0000-0000-000000000501	69800	2026-07-29 05:27:30.418295+00	00000000-0000-0000-0000-000000000001
d66048f4-db92-4eca-9e0b-1626e19046f0	POS-20260729053301076	67222182-84f3-416a-953f-e21a02577d13	00000000-0000-0000-0000-000000000501	23800	2026-07-29 05:34:26.477491+00	00000000-0000-0000-0000-000000000001
5280a2c9-4ef5-4724-95fe-fb3630b67d10	POS-20260729053452286	67222182-84f3-416a-953f-e21a02577d13	00000000-0000-0000-0000-000000000501	40000	2026-07-29 05:35:23.366745+00	00000000-0000-0000-0000-000000000001
d436ef01-55b7-4828-96bf-56e2c79fe309	POS-20260730131232419	67222182-84f3-416a-953f-e21a02577d13	00000000-0000-0000-0000-000000000501	1800	2026-07-30 13:12:46.874679+00	00000000-0000-0000-0000-000000000001
f0dd1437-49fb-4e59-a372-1de8e3f5ab87	POS-20260729161859674	67222182-84f3-416a-953f-e21a02577d13	\N	40000	2026-07-30 13:12:59.849597+00	00000000-0000-0000-0000-000000000001
f7b44cf0-2fe1-4e4e-a408-e6b1afe6eca2	POS-20260801203114999	67222182-84f3-416a-953f-e21a02577d13	2cd5a00a-3c63-4b20-bbab-8e225c89a1ed	43000	2026-08-01 20:31:34.877208+00	00000000-0000-0000-0000-000000000001
\.


--
-- Data for Name: shift_expenses; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.shift_expenses (id, shift_id, voucher_no, description, amount_paisa, expense_category, receipt_reference, payment_method, is_recurring, logged_by_user_id, logged_at, tenant_id) FROM stdin;
f1ee3d64-e712-4e99-8231-39dedc300a66	\N	EXP-20260801-170719-7215	fridge	90000	MAINTENANCE	001	CASH	f	7025ab9a-f599-4b3b-8e56-9e008e81feec	2026-08-01 17:07:19.584286+00	00000000-0000-0000-0000-000000000001
\.


--
-- Data for Name: tenants; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.tenants (id, name, slug, is_active, created_at) FROM stdin;
00000000-0000-0000-0000-000000000001	Master Tenant	master	t	2026-07-16 00:00:00+00
\.


--
-- Data for Name: terminals; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.terminals (id, terminal_name, mac_address, is_active, last_sync_time, tenant_id) FROM stdin;
00000000-0000-0000-0000-000000000010	Copenhagen Mart POS	PILOT-TERMINAL-001	t	2026-07-25 11:13:08.767357+00	00000000-0000-0000-0000-000000000001
\.


--
-- Data for Name: users; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.users (id, username, password_hash, role_id, is_active, created_at, updated_at, pin_hash, tenant_id) FROM stdin;
67222182-84f3-416a-953f-e21a02577d13	cashier	WEBPOS$100000$XdWKsnrSP69WkIrPvzGKVA==$PsOzmh7/Lkv6F0YTYH3geP5grAqOsZTQGfZ4D34bHEI=	c42f74b9-b665-477e-8c5a-2eddeee6e085	t	2026-07-25 11:13:08.767357+00	2026-07-25 11:13:08.767357+00	698D5D6DFB11B753D455426A8A4837019B5A1B1E8E398514BD242B433853E0E8	00000000-0000-0000-0000-000000000001
75146367-b621-4ab8-9f0b-d46169802382	abc	WEBPOS$100000$cu13OROOZh7kCjHZ5DGuxg==$vGm6T9D5dRaaWm9nmPzaAZ/L78OIlB4h3L8lvfTqF00=	c42f74b9-b665-477e-8c5a-2eddeee6e085	t	2026-07-25 11:13:08.767357+00	2026-07-25 11:13:08.767357+00	40E5A5FDFBD29000D05C371D4BC4D9167ADB73760A279595A238DBEEA3C2F5D3	00000000-0000-0000-0000-000000000001
7025ab9a-f599-4b3b-8e56-9e008e81feec	admin	WEBPOS$100000$4XgWWVncCt3WnOManWIlQw==$xuKTsE02LPKo9P5g1mhNgP2ZJbdlyLJ9cSx/TiOwLFI=	1fc4e707-cb9d-4bea-a209-00279add3b0d	t	2026-07-25 11:13:08.767357+00	2026-08-01 21:23:14.112993+00	8BAA45F3B3369CAAEC85286B792C06928BDE546F0B2E6145535D2CEF20D933BF	00000000-0000-0000-0000-000000000001
c72f658e-0ac7-4186-b298-d862fd3d7bd2	ammar	WEBPOS$100000$re5h4UCL7IopUfZX8fvnvQ==$DnSyCSnFvFeAa3R6H41ksfK9eRGqRIVzMEpHDCdE8fA=	f3f36891-33a8-4376-b7da-ebe59fb1edee	t	2026-07-25 11:13:08.767357+00	2026-08-01 21:23:14.112993+00	E2036221DDDF0C86F582B74BC9246069EC532A75AF0B1A48F27EAEA4D5305D6F	00000000-0000-0000-0000-000000000001
\.


--
-- Name: __EFMigrationsHistory PK___EFMigrationsHistory; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."__EFMigrationsHistory"
    ADD CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId");


--
-- Name: cashier_shifts PK_cashier_shifts; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.cashier_shifts
    ADD CONSTRAINT "PK_cashier_shifts" PRIMARY KEY (id);


--
-- Name: categories PK_categories; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.categories
    ADD CONSTRAINT "PK_categories" PRIMARY KEY (id);


--
-- Name: daily_milk_collections PK_daily_milk_collections; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.daily_milk_collections
    ADD CONSTRAINT "PK_daily_milk_collections" PRIMARY KEY (id);


--
-- Name: damaged_stock_logs PK_damaged_stock_logs; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.damaged_stock_logs
    ADD CONSTRAINT "PK_damaged_stock_logs" PRIMARY KEY (id);


--
-- Name: general_ledger_entries PK_general_ledger_entries; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.general_ledger_entries
    ADD CONSTRAINT "PK_general_ledger_entries" PRIMARY KEY (id);


--
-- Name: parties PK_parties; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.parties
    ADD CONSTRAINT "PK_parties" PRIMARY KEY (id);


--
-- Name: party_ledgers PK_party_ledgers; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.party_ledgers
    ADD CONSTRAINT "PK_party_ledgers" PRIMARY KEY (id);


--
-- Name: party_payment_allocations PK_party_payment_allocations; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.party_payment_allocations
    ADD CONSTRAINT "PK_party_payment_allocations" PRIMARY KEY (id);


--
-- Name: product_batches PK_product_batches; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.product_batches
    ADD CONSTRAINT "PK_product_batches" PRIMARY KEY (id);


--
-- Name: production_consumption_items PK_production_consumption_items; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.production_consumption_items
    ADD CONSTRAINT "PK_production_consumption_items" PRIMARY KEY (id);


--
-- Name: production_logs PK_production_logs; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.production_logs
    ADD CONSTRAINT "PK_production_logs" PRIMARY KEY (id);


--
-- Name: production_yield_items PK_production_yield_items; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.production_yield_items
    ADD CONSTRAINT "PK_production_yield_items" PRIMARY KEY (id);


--
-- Name: products PK_products; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.products
    ADD CONSTRAINT "PK_products" PRIMARY KEY (id);


--
-- Name: purchase_items PK_purchase_items; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.purchase_items
    ADD CONSTRAINT "PK_purchase_items" PRIMARY KEY (id);


--
-- Name: purchase_orders PK_purchase_orders; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.purchase_orders
    ADD CONSTRAINT "PK_purchase_orders" PRIMARY KEY (id);


--
-- Name: purchase_return_items PK_purchase_return_items; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.purchase_return_items
    ADD CONSTRAINT "PK_purchase_return_items" PRIMARY KEY (id);


--
-- Name: purchase_returns PK_purchase_returns; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.purchase_returns
    ADD CONSTRAINT "PK_purchase_returns" PRIMARY KEY (id);


--
-- Name: roles PK_roles; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.roles
    ADD CONSTRAINT "PK_roles" PRIMARY KEY (id);


--
-- Name: sales_invoices PK_sales_invoices; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales_invoices
    ADD CONSTRAINT "PK_sales_invoices" PRIMARY KEY (invoice_no);


--
-- Name: sales_items PK_sales_items; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales_items
    ADD CONSTRAINT "PK_sales_items" PRIMARY KEY (id);


--
-- Name: sales_return_items PK_sales_return_items; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales_return_items
    ADD CONSTRAINT "PK_sales_return_items" PRIMARY KEY (id);


--
-- Name: sales_returns PK_sales_returns; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales_returns
    ADD CONSTRAINT "PK_sales_returns" PRIMARY KEY (id);


--
-- Name: shift_expenses PK_shift_expenses; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.shift_expenses
    ADD CONSTRAINT "PK_shift_expenses" PRIMARY KEY (id);


--
-- Name: tenants PK_tenants; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.tenants
    ADD CONSTRAINT "PK_tenants" PRIMARY KEY (id);


--
-- Name: terminals PK_terminals; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.terminals
    ADD CONSTRAINT "PK_terminals" PRIMARY KEY (id);


--
-- Name: users PK_users; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT "PK_users" PRIMARY KEY (id);


--
-- Name: IX_cashier_shifts_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_cashier_shifts_tenant_id" ON public.cashier_shifts USING btree (tenant_id);


--
-- Name: IX_categories_parent_category_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_categories_parent_category_id" ON public.categories USING btree (parent_category_id);


--
-- Name: IX_categories_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_categories_tenant_id" ON public.categories USING btree (tenant_id);


--
-- Name: IX_daily_milk_collections_supplier_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_daily_milk_collections_supplier_id" ON public.daily_milk_collections USING btree (supplier_id);


--
-- Name: IX_daily_milk_collections_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_daily_milk_collections_tenant_id" ON public.daily_milk_collections USING btree (tenant_id);


--
-- Name: IX_damaged_stock_logs_batch_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_damaged_stock_logs_batch_id" ON public.damaged_stock_logs USING btree (batch_id);


--
-- Name: IX_damaged_stock_logs_logged_by; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_damaged_stock_logs_logged_by" ON public.damaged_stock_logs USING btree (logged_by);


--
-- Name: IX_damaged_stock_logs_product_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_damaged_stock_logs_product_id" ON public.damaged_stock_logs USING btree (product_id);


--
-- Name: IX_damaged_stock_logs_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_damaged_stock_logs_tenant_id" ON public.damaged_stock_logs USING btree (tenant_id);


--
-- Name: IX_general_ledger_entries_account_code; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_general_ledger_entries_account_code" ON public.general_ledger_entries USING btree (account_code);


--
-- Name: IX_general_ledger_entries_created_at; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_general_ledger_entries_created_at" ON public.general_ledger_entries USING btree (created_at);


--
-- Name: IX_general_ledger_entries_reference_no; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_general_ledger_entries_reference_no" ON public.general_ledger_entries USING btree (reference_no);


--
-- Name: IX_general_ledger_entries_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_general_ledger_entries_tenant_id" ON public.general_ledger_entries USING btree (tenant_id);


--
-- Name: IX_general_ledger_entries_transaction_group_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_general_ledger_entries_transaction_group_id" ON public.general_ledger_entries USING btree (transaction_group_id);


--
-- Name: IX_parties_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_parties_tenant_id" ON public.parties USING btree (tenant_id);


--
-- Name: IX_parties_tenant_updated_at; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_parties_tenant_updated_at" ON public.parties USING btree (tenant_id, updated_at);


--
-- Name: IX_party_ledgers_invoice_no; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_party_ledgers_invoice_no" ON public.party_ledgers USING btree (invoice_no);


--
-- Name: IX_party_ledgers_party_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_party_ledgers_party_id" ON public.party_ledgers USING btree (party_id);


--
-- Name: IX_party_ledgers_purchase_order_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_party_ledgers_purchase_order_id" ON public.party_ledgers USING btree (purchase_order_id);


--
-- Name: IX_party_ledgers_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_party_ledgers_tenant_id" ON public.party_ledgers USING btree (tenant_id);


--
-- Name: IX_party_payment_allocations_invoice_no; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_party_payment_allocations_invoice_no" ON public.party_payment_allocations USING btree (invoice_no);


--
-- Name: IX_party_payment_allocations_party_ledger_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_party_payment_allocations_party_ledger_id" ON public.party_payment_allocations USING btree (party_ledger_id);


--
-- Name: IX_party_payment_allocations_purchase_order_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_party_payment_allocations_purchase_order_id" ON public.party_payment_allocations USING btree (purchase_order_id);


--
-- Name: IX_party_payment_allocations_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_party_payment_allocations_tenant_id" ON public.party_payment_allocations USING btree (tenant_id);


--
-- Name: IX_product_batches_product_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_product_batches_product_id" ON public.product_batches USING btree (product_id);


--
-- Name: IX_product_batches_purchase_order_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_product_batches_purchase_order_id" ON public.product_batches USING btree (purchase_order_id);


--
-- Name: IX_product_batches_supplier_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_product_batches_supplier_id" ON public.product_batches USING btree (supplier_id);


--
-- Name: IX_product_batches_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_product_batches_tenant_id" ON public.product_batches USING btree (tenant_id);


--
-- Name: IX_production_consumption_items_product_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_production_consumption_items_product_id" ON public.production_consumption_items USING btree (product_id);


--
-- Name: IX_production_consumption_items_production_log_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_production_consumption_items_production_log_id" ON public.production_consumption_items USING btree (production_log_id);


--
-- Name: IX_production_consumption_items_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_production_consumption_items_tenant_id" ON public.production_consumption_items USING btree (tenant_id);


--
-- Name: IX_production_logs_batch_reference; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_production_logs_batch_reference" ON public.production_logs USING btree (batch_reference);


--
-- Name: IX_production_logs_operator_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_production_logs_operator_id" ON public.production_logs USING btree (operator_id);


--
-- Name: IX_production_logs_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_production_logs_tenant_id" ON public.production_logs USING btree (tenant_id);


--
-- Name: IX_production_yield_items_product_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_production_yield_items_product_id" ON public.production_yield_items USING btree (product_id);


--
-- Name: IX_production_yield_items_production_log_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_production_yield_items_production_log_id" ON public.production_yield_items USING btree (production_log_id);


--
-- Name: IX_production_yield_items_target_batch_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_production_yield_items_target_batch_id" ON public.production_yield_items USING btree (target_batch_id);


--
-- Name: IX_production_yield_items_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_production_yield_items_tenant_id" ON public.production_yield_items USING btree (tenant_id);


--
-- Name: IX_products_barcode; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_products_barcode" ON public.products USING btree (barcode);


--
-- Name: IX_products_category_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_products_category_id" ON public.products USING btree (category_id);


--
-- Name: IX_products_sku; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_products_sku" ON public.products USING btree (sku);


--
-- Name: IX_products_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_products_tenant_id" ON public.products USING btree (tenant_id);


--
-- Name: IX_products_tenant_id_short_code; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_products_tenant_id_short_code" ON public.products USING btree (tenant_id, short_code) WHERE ((short_code)::text <> ''::text);


--
-- Name: IX_products_tenant_updated_at; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_products_tenant_updated_at" ON public.products USING btree (tenant_id, updated_at);


--
-- Name: IX_purchase_items_batch_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_purchase_items_batch_id" ON public.purchase_items USING btree (batch_id);


--
-- Name: IX_purchase_items_product_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_purchase_items_product_id" ON public.purchase_items USING btree (product_id);


--
-- Name: IX_purchase_items_purchase_order_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_purchase_items_purchase_order_id" ON public.purchase_items USING btree (purchase_order_id);


--
-- Name: IX_purchase_items_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_purchase_items_tenant_id" ON public.purchase_items USING btree (tenant_id);


--
-- Name: IX_purchase_orders_receiver_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_purchase_orders_receiver_id" ON public.purchase_orders USING btree (receiver_id);


--
-- Name: IX_purchase_orders_supplier_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_purchase_orders_supplier_id" ON public.purchase_orders USING btree (supplier_id);


--
-- Name: IX_purchase_orders_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_purchase_orders_tenant_id" ON public.purchase_orders USING btree (tenant_id);


--
-- Name: IX_purchase_return_items_batch_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_purchase_return_items_batch_id" ON public.purchase_return_items USING btree (batch_id);


--
-- Name: IX_purchase_return_items_product_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_purchase_return_items_product_id" ON public.purchase_return_items USING btree (product_id);


--
-- Name: IX_purchase_return_items_purchase_return_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_purchase_return_items_purchase_return_id" ON public.purchase_return_items USING btree (purchase_return_id);


--
-- Name: IX_purchase_return_items_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_purchase_return_items_tenant_id" ON public.purchase_return_items USING btree (tenant_id);


--
-- Name: IX_purchase_returns_manager_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_purchase_returns_manager_id" ON public.purchase_returns USING btree (manager_id);


--
-- Name: IX_purchase_returns_original_purchase_order_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_purchase_returns_original_purchase_order_id" ON public.purchase_returns USING btree (original_purchase_order_id);


--
-- Name: IX_purchase_returns_supplier_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_purchase_returns_supplier_id" ON public.purchase_returns USING btree (supplier_id);


--
-- Name: IX_purchase_returns_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_purchase_returns_tenant_id" ON public.purchase_returns USING btree (tenant_id);


--
-- Name: IX_roles_role_name; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_roles_role_name" ON public.roles USING btree (role_name);


--
-- Name: IX_roles_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_roles_tenant_id" ON public.roles USING btree (tenant_id);


--
-- Name: IX_sales_invoices_cashier_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_sales_invoices_cashier_id" ON public.sales_invoices USING btree (cashier_id);


--
-- Name: IX_sales_invoices_customer_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_sales_invoices_customer_id" ON public.sales_invoices USING btree (customer_id);


--
-- Name: IX_sales_invoices_shift_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_sales_invoices_shift_id" ON public.sales_invoices USING btree (shift_id);


--
-- Name: IX_sales_invoices_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_sales_invoices_tenant_id" ON public.sales_invoices USING btree (tenant_id);


--
-- Name: IX_sales_invoices_terminal_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_sales_invoices_terminal_id" ON public.sales_invoices USING btree (terminal_id);


--
-- Name: IX_sales_items_batch_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_sales_items_batch_id" ON public.sales_items USING btree (batch_id);


--
-- Name: IX_sales_items_invoice_no; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_sales_items_invoice_no" ON public.sales_items USING btree (invoice_no);


--
-- Name: IX_sales_items_product_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_sales_items_product_id" ON public.sales_items USING btree (product_id);


--
-- Name: IX_sales_items_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_sales_items_tenant_id" ON public.sales_items USING btree (tenant_id);


--
-- Name: IX_sales_return_items_batch_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_sales_return_items_batch_id" ON public.sales_return_items USING btree (batch_id);


--
-- Name: IX_sales_return_items_product_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_sales_return_items_product_id" ON public.sales_return_items USING btree (product_id);


--
-- Name: IX_sales_return_items_sales_return_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_sales_return_items_sales_return_id" ON public.sales_return_items USING btree (sales_return_id);


--
-- Name: IX_sales_return_items_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_sales_return_items_tenant_id" ON public.sales_return_items USING btree (tenant_id);


--
-- Name: IX_sales_returns_cashier_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_sales_returns_cashier_id" ON public.sales_returns USING btree (cashier_id);


--
-- Name: IX_sales_returns_customer_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_sales_returns_customer_id" ON public.sales_returns USING btree (customer_id);


--
-- Name: IX_sales_returns_original_invoice_no; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_sales_returns_original_invoice_no" ON public.sales_returns USING btree (original_invoice_no);


--
-- Name: IX_sales_returns_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_sales_returns_tenant_id" ON public.sales_returns USING btree (tenant_id);


--
-- Name: IX_shift_expenses_expense_category; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_shift_expenses_expense_category" ON public.shift_expenses USING btree (expense_category);


--
-- Name: IX_shift_expenses_logged_by_user_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_shift_expenses_logged_by_user_id" ON public.shift_expenses USING btree (logged_by_user_id);


--
-- Name: IX_shift_expenses_shift_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_shift_expenses_shift_id" ON public.shift_expenses USING btree (shift_id);


--
-- Name: IX_shift_expenses_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_shift_expenses_tenant_id" ON public.shift_expenses USING btree (tenant_id);


--
-- Name: IX_shift_expenses_voucher_no; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_shift_expenses_voucher_no" ON public.shift_expenses USING btree (voucher_no);


--
-- Name: IX_tenants_slug; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_tenants_slug" ON public.tenants USING btree (slug);


--
-- Name: IX_terminals_mac_address; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_terminals_mac_address" ON public.terminals USING btree (mac_address);


--
-- Name: IX_terminals_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_terminals_tenant_id" ON public.terminals USING btree (tenant_id);


--
-- Name: IX_terminals_terminal_name; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_terminals_terminal_name" ON public.terminals USING btree (terminal_name);


--
-- Name: IX_users_pin_hash; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_users_pin_hash" ON public.users USING btree (pin_hash) WHERE ((pin_hash)::text <> ''::text);


--
-- Name: IX_users_role_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_users_role_id" ON public.users USING btree (role_id);


--
-- Name: IX_users_tenant_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_users_tenant_id" ON public.users USING btree (tenant_id);


--
-- Name: IX_users_username; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_users_username" ON public.users USING btree (username);


--
-- Name: UX_cashier_shifts_open_cashier; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "UX_cashier_shifts_open_cashier" ON public.cashier_shifts USING btree (cashier_id) WHERE ((status)::text = 'OPEN'::text);


--
-- Name: UX_cashier_shifts_open_terminal; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "UX_cashier_shifts_open_terminal" ON public.cashier_shifts USING btree (terminal_id) WHERE ((status)::text = 'OPEN'::text);


--
-- Name: UX_parties_tenant_phone; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "UX_parties_tenant_phone" ON public.parties USING btree (tenant_id, phone_number);


--
-- Name: UX_parties_tenant_type_name; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "UX_parties_tenant_type_name" ON public.parties USING btree (tenant_id, party_type, name);


--
-- Name: cashier_shifts FK_cashier_shifts_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.cashier_shifts
    ADD CONSTRAINT "FK_cashier_shifts_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: cashier_shifts FK_cashier_shifts_terminals_terminal_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.cashier_shifts
    ADD CONSTRAINT "FK_cashier_shifts_terminals_terminal_id" FOREIGN KEY (terminal_id) REFERENCES public.terminals(id) ON DELETE RESTRICT;


--
-- Name: cashier_shifts FK_cashier_shifts_users_cashier_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.cashier_shifts
    ADD CONSTRAINT "FK_cashier_shifts_users_cashier_id" FOREIGN KEY (cashier_id) REFERENCES public.users(id) ON DELETE RESTRICT;


--
-- Name: categories FK_categories_categories_parent_category_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.categories
    ADD CONSTRAINT "FK_categories_categories_parent_category_id" FOREIGN KEY (parent_category_id) REFERENCES public.categories(id) ON DELETE RESTRICT;


--
-- Name: categories FK_categories_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.categories
    ADD CONSTRAINT "FK_categories_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: daily_milk_collections FK_daily_milk_collections_parties_supplier_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.daily_milk_collections
    ADD CONSTRAINT "FK_daily_milk_collections_parties_supplier_id" FOREIGN KEY (supplier_id) REFERENCES public.parties(id) ON DELETE RESTRICT;


--
-- Name: daily_milk_collections FK_daily_milk_collections_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.daily_milk_collections
    ADD CONSTRAINT "FK_daily_milk_collections_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: damaged_stock_logs FK_damaged_stock_logs_product_batches_batch_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.damaged_stock_logs
    ADD CONSTRAINT "FK_damaged_stock_logs_product_batches_batch_id" FOREIGN KEY (batch_id) REFERENCES public.product_batches(id) ON DELETE RESTRICT;


--
-- Name: damaged_stock_logs FK_damaged_stock_logs_products_product_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.damaged_stock_logs
    ADD CONSTRAINT "FK_damaged_stock_logs_products_product_id" FOREIGN KEY (product_id) REFERENCES public.products(id) ON DELETE RESTRICT;


--
-- Name: damaged_stock_logs FK_damaged_stock_logs_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.damaged_stock_logs
    ADD CONSTRAINT "FK_damaged_stock_logs_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: damaged_stock_logs FK_damaged_stock_logs_users_logged_by; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.damaged_stock_logs
    ADD CONSTRAINT "FK_damaged_stock_logs_users_logged_by" FOREIGN KEY (logged_by) REFERENCES public.users(id) ON DELETE RESTRICT;


--
-- Name: general_ledger_entries FK_general_ledger_entries_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.general_ledger_entries
    ADD CONSTRAINT "FK_general_ledger_entries_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: parties FK_parties_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.parties
    ADD CONSTRAINT "FK_parties_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: party_ledgers FK_party_ledgers_parties_party_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.party_ledgers
    ADD CONSTRAINT "FK_party_ledgers_parties_party_id" FOREIGN KEY (party_id) REFERENCES public.parties(id) ON DELETE CASCADE;


--
-- Name: party_ledgers FK_party_ledgers_purchase_orders_purchase_order_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.party_ledgers
    ADD CONSTRAINT "FK_party_ledgers_purchase_orders_purchase_order_id" FOREIGN KEY (purchase_order_id) REFERENCES public.purchase_orders(id) ON DELETE RESTRICT;


--
-- Name: party_ledgers FK_party_ledgers_sales_invoices_invoice_no; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.party_ledgers
    ADD CONSTRAINT "FK_party_ledgers_sales_invoices_invoice_no" FOREIGN KEY (invoice_no) REFERENCES public.sales_invoices(invoice_no) ON DELETE RESTRICT;


--
-- Name: party_ledgers FK_party_ledgers_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.party_ledgers
    ADD CONSTRAINT "FK_party_ledgers_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: party_payment_allocations FK_party_payment_allocations_party_ledgers_party_ledger_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.party_payment_allocations
    ADD CONSTRAINT "FK_party_payment_allocations_party_ledgers_party_ledger_id" FOREIGN KEY (party_ledger_id) REFERENCES public.party_ledgers(id) ON DELETE CASCADE;


--
-- Name: party_payment_allocations FK_party_payment_allocations_purchase_orders_purchase_order_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.party_payment_allocations
    ADD CONSTRAINT "FK_party_payment_allocations_purchase_orders_purchase_order_id" FOREIGN KEY (purchase_order_id) REFERENCES public.purchase_orders(id) ON DELETE RESTRICT;


--
-- Name: party_payment_allocations FK_party_payment_allocations_sales_invoices_invoice_no; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.party_payment_allocations
    ADD CONSTRAINT "FK_party_payment_allocations_sales_invoices_invoice_no" FOREIGN KEY (invoice_no) REFERENCES public.sales_invoices(invoice_no) ON DELETE RESTRICT;


--
-- Name: party_payment_allocations FK_party_payment_allocations_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.party_payment_allocations
    ADD CONSTRAINT "FK_party_payment_allocations_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: product_batches FK_product_batches_parties_supplier_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.product_batches
    ADD CONSTRAINT "FK_product_batches_parties_supplier_id" FOREIGN KEY (supplier_id) REFERENCES public.parties(id) ON DELETE RESTRICT;


--
-- Name: product_batches FK_product_batches_products_product_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.product_batches
    ADD CONSTRAINT "FK_product_batches_products_product_id" FOREIGN KEY (product_id) REFERENCES public.products(id) ON DELETE RESTRICT;


--
-- Name: product_batches FK_product_batches_purchase_orders_purchase_order_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.product_batches
    ADD CONSTRAINT "FK_product_batches_purchase_orders_purchase_order_id" FOREIGN KEY (purchase_order_id) REFERENCES public.purchase_orders(id) ON DELETE RESTRICT;


--
-- Name: product_batches FK_product_batches_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.product_batches
    ADD CONSTRAINT "FK_product_batches_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: production_consumption_items FK_production_consumption_items_production_logs_production_log~; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.production_consumption_items
    ADD CONSTRAINT "FK_production_consumption_items_production_logs_production_log~" FOREIGN KEY (production_log_id) REFERENCES public.production_logs(id) ON DELETE CASCADE;


--
-- Name: production_consumption_items FK_production_consumption_items_products_product_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.production_consumption_items
    ADD CONSTRAINT "FK_production_consumption_items_products_product_id" FOREIGN KEY (product_id) REFERENCES public.products(id) ON DELETE RESTRICT;


--
-- Name: production_consumption_items FK_production_consumption_items_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.production_consumption_items
    ADD CONSTRAINT "FK_production_consumption_items_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: production_logs FK_production_logs_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.production_logs
    ADD CONSTRAINT "FK_production_logs_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: production_logs FK_production_logs_users_operator_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.production_logs
    ADD CONSTRAINT "FK_production_logs_users_operator_id" FOREIGN KEY (operator_id) REFERENCES public.users(id) ON DELETE RESTRICT;


--
-- Name: production_yield_items FK_production_yield_items_product_batches_target_batch_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.production_yield_items
    ADD CONSTRAINT "FK_production_yield_items_product_batches_target_batch_id" FOREIGN KEY (target_batch_id) REFERENCES public.product_batches(id) ON DELETE RESTRICT;


--
-- Name: production_yield_items FK_production_yield_items_production_logs_production_log_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.production_yield_items
    ADD CONSTRAINT "FK_production_yield_items_production_logs_production_log_id" FOREIGN KEY (production_log_id) REFERENCES public.production_logs(id) ON DELETE CASCADE;


--
-- Name: production_yield_items FK_production_yield_items_products_product_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.production_yield_items
    ADD CONSTRAINT "FK_production_yield_items_products_product_id" FOREIGN KEY (product_id) REFERENCES public.products(id) ON DELETE RESTRICT;


--
-- Name: production_yield_items FK_production_yield_items_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.production_yield_items
    ADD CONSTRAINT "FK_production_yield_items_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: products FK_products_categories_category_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.products
    ADD CONSTRAINT "FK_products_categories_category_id" FOREIGN KEY (category_id) REFERENCES public.categories(id) ON DELETE RESTRICT;


--
-- Name: products FK_products_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.products
    ADD CONSTRAINT "FK_products_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: purchase_items FK_purchase_items_product_batches_batch_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.purchase_items
    ADD CONSTRAINT "FK_purchase_items_product_batches_batch_id" FOREIGN KEY (batch_id) REFERENCES public.product_batches(id) ON DELETE RESTRICT;


--
-- Name: purchase_items FK_purchase_items_products_product_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.purchase_items
    ADD CONSTRAINT "FK_purchase_items_products_product_id" FOREIGN KEY (product_id) REFERENCES public.products(id) ON DELETE RESTRICT;


--
-- Name: purchase_items FK_purchase_items_purchase_orders_purchase_order_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.purchase_items
    ADD CONSTRAINT "FK_purchase_items_purchase_orders_purchase_order_id" FOREIGN KEY (purchase_order_id) REFERENCES public.purchase_orders(id) ON DELETE CASCADE;


--
-- Name: purchase_items FK_purchase_items_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.purchase_items
    ADD CONSTRAINT "FK_purchase_items_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: purchase_orders FK_purchase_orders_parties_supplier_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.purchase_orders
    ADD CONSTRAINT "FK_purchase_orders_parties_supplier_id" FOREIGN KEY (supplier_id) REFERENCES public.parties(id) ON DELETE RESTRICT;


--
-- Name: purchase_orders FK_purchase_orders_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.purchase_orders
    ADD CONSTRAINT "FK_purchase_orders_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: purchase_orders FK_purchase_orders_users_receiver_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.purchase_orders
    ADD CONSTRAINT "FK_purchase_orders_users_receiver_id" FOREIGN KEY (receiver_id) REFERENCES public.users(id) ON DELETE RESTRICT;


--
-- Name: purchase_return_items FK_purchase_return_items_product_batches_batch_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.purchase_return_items
    ADD CONSTRAINT "FK_purchase_return_items_product_batches_batch_id" FOREIGN KEY (batch_id) REFERENCES public.product_batches(id) ON DELETE RESTRICT;


--
-- Name: purchase_return_items FK_purchase_return_items_products_product_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.purchase_return_items
    ADD CONSTRAINT "FK_purchase_return_items_products_product_id" FOREIGN KEY (product_id) REFERENCES public.products(id) ON DELETE RESTRICT;


--
-- Name: purchase_return_items FK_purchase_return_items_purchase_returns_purchase_return_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.purchase_return_items
    ADD CONSTRAINT "FK_purchase_return_items_purchase_returns_purchase_return_id" FOREIGN KEY (purchase_return_id) REFERENCES public.purchase_returns(id) ON DELETE CASCADE;


--
-- Name: purchase_return_items FK_purchase_return_items_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.purchase_return_items
    ADD CONSTRAINT "FK_purchase_return_items_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: purchase_returns FK_purchase_returns_parties_supplier_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.purchase_returns
    ADD CONSTRAINT "FK_purchase_returns_parties_supplier_id" FOREIGN KEY (supplier_id) REFERENCES public.parties(id) ON DELETE RESTRICT;


--
-- Name: purchase_returns FK_purchase_returns_purchase_orders_original_purchase_order_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.purchase_returns
    ADD CONSTRAINT "FK_purchase_returns_purchase_orders_original_purchase_order_id" FOREIGN KEY (original_purchase_order_id) REFERENCES public.purchase_orders(id) ON DELETE RESTRICT;


--
-- Name: purchase_returns FK_purchase_returns_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.purchase_returns
    ADD CONSTRAINT "FK_purchase_returns_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: purchase_returns FK_purchase_returns_users_manager_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.purchase_returns
    ADD CONSTRAINT "FK_purchase_returns_users_manager_id" FOREIGN KEY (manager_id) REFERENCES public.users(id) ON DELETE RESTRICT;


--
-- Name: roles FK_roles_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.roles
    ADD CONSTRAINT "FK_roles_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: sales_invoices FK_sales_invoices_cashier_shifts_shift_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales_invoices
    ADD CONSTRAINT "FK_sales_invoices_cashier_shifts_shift_id" FOREIGN KEY (shift_id) REFERENCES public.cashier_shifts(id) ON DELETE RESTRICT;


--
-- Name: sales_invoices FK_sales_invoices_parties_customer_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales_invoices
    ADD CONSTRAINT "FK_sales_invoices_parties_customer_id" FOREIGN KEY (customer_id) REFERENCES public.parties(id) ON DELETE RESTRICT;


--
-- Name: sales_invoices FK_sales_invoices_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales_invoices
    ADD CONSTRAINT "FK_sales_invoices_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: sales_invoices FK_sales_invoices_terminals_terminal_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales_invoices
    ADD CONSTRAINT "FK_sales_invoices_terminals_terminal_id" FOREIGN KEY (terminal_id) REFERENCES public.terminals(id) ON DELETE RESTRICT;


--
-- Name: sales_invoices FK_sales_invoices_users_cashier_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales_invoices
    ADD CONSTRAINT "FK_sales_invoices_users_cashier_id" FOREIGN KEY (cashier_id) REFERENCES public.users(id) ON DELETE RESTRICT;


--
-- Name: sales_items FK_sales_items_product_batches_batch_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales_items
    ADD CONSTRAINT "FK_sales_items_product_batches_batch_id" FOREIGN KEY (batch_id) REFERENCES public.product_batches(id) ON DELETE RESTRICT;


--
-- Name: sales_items FK_sales_items_products_product_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales_items
    ADD CONSTRAINT "FK_sales_items_products_product_id" FOREIGN KEY (product_id) REFERENCES public.products(id) ON DELETE RESTRICT;


--
-- Name: sales_items FK_sales_items_sales_invoices_invoice_no; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales_items
    ADD CONSTRAINT "FK_sales_items_sales_invoices_invoice_no" FOREIGN KEY (invoice_no) REFERENCES public.sales_invoices(invoice_no) ON DELETE CASCADE;


--
-- Name: sales_items FK_sales_items_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales_items
    ADD CONSTRAINT "FK_sales_items_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: sales_return_items FK_sales_return_items_product_batches_batch_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales_return_items
    ADD CONSTRAINT "FK_sales_return_items_product_batches_batch_id" FOREIGN KEY (batch_id) REFERENCES public.product_batches(id) ON DELETE RESTRICT;


--
-- Name: sales_return_items FK_sales_return_items_products_product_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales_return_items
    ADD CONSTRAINT "FK_sales_return_items_products_product_id" FOREIGN KEY (product_id) REFERENCES public.products(id) ON DELETE RESTRICT;


--
-- Name: sales_return_items FK_sales_return_items_sales_returns_sales_return_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales_return_items
    ADD CONSTRAINT "FK_sales_return_items_sales_returns_sales_return_id" FOREIGN KEY (sales_return_id) REFERENCES public.sales_returns(id) ON DELETE CASCADE;


--
-- Name: sales_return_items FK_sales_return_items_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales_return_items
    ADD CONSTRAINT "FK_sales_return_items_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: sales_returns FK_sales_returns_parties_customer_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales_returns
    ADD CONSTRAINT "FK_sales_returns_parties_customer_id" FOREIGN KEY (customer_id) REFERENCES public.parties(id) ON DELETE RESTRICT;


--
-- Name: sales_returns FK_sales_returns_sales_invoices_original_invoice_no; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales_returns
    ADD CONSTRAINT "FK_sales_returns_sales_invoices_original_invoice_no" FOREIGN KEY (original_invoice_no) REFERENCES public.sales_invoices(invoice_no) ON DELETE RESTRICT;


--
-- Name: sales_returns FK_sales_returns_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales_returns
    ADD CONSTRAINT "FK_sales_returns_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: sales_returns FK_sales_returns_users_cashier_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales_returns
    ADD CONSTRAINT "FK_sales_returns_users_cashier_id" FOREIGN KEY (cashier_id) REFERENCES public.users(id) ON DELETE RESTRICT;


--
-- Name: shift_expenses FK_shift_expenses_cashier_shifts_shift_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.shift_expenses
    ADD CONSTRAINT "FK_shift_expenses_cashier_shifts_shift_id" FOREIGN KEY (shift_id) REFERENCES public.cashier_shifts(id) ON DELETE RESTRICT;


--
-- Name: shift_expenses FK_shift_expenses_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.shift_expenses
    ADD CONSTRAINT "FK_shift_expenses_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: shift_expenses FK_shift_expenses_users_logged_by_user_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.shift_expenses
    ADD CONSTRAINT "FK_shift_expenses_users_logged_by_user_id" FOREIGN KEY (logged_by_user_id) REFERENCES public.users(id) ON DELETE RESTRICT;


--
-- Name: terminals FK_terminals_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.terminals
    ADD CONSTRAINT "FK_terminals_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- Name: users FK_users_roles_role_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT "FK_users_roles_role_id" FOREIGN KEY (role_id) REFERENCES public.roles(id) ON DELETE RESTRICT;


--
-- Name: users FK_users_tenants_tenant_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT "FK_users_tenants_tenant_id" FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE RESTRICT;


--
-- PostgreSQL database dump complete
--

\unrestrict RFWcVV4ZzHfoepFc93f0gKDaYsf0Rc8gRdg7x09oFdg3YPreB3cymKfuwdKt13e

--
-- Database "postgres" dump
--

\connect postgres

--
-- PostgreSQL database dump
--

\restrict dow7j3u3we0mScolCNose03QuOcgO13syq7fyiXM3dejAXlM2bCxgBV0lPxc4SC

-- Dumped from database version 16.14
-- Dumped by pg_dump version 16.14

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- PostgreSQL database dump complete
--

\unrestrict dow7j3u3we0mScolCNose03QuOcgO13syq7fyiXM3dejAXlM2bCxgBV0lPxc4SC

--
-- PostgreSQL database cluster dump complete
--

